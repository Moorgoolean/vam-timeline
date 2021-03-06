﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class PeerManager
    {
        public AtomAnimationEditContext animationEditContext;
        public AtomAnimation animation => animationEditContext.animation;
        public bool syncing => _sending > 0 || _receiving;

        private readonly List<JSONStorable> _peers = new List<JSONStorable>();
        private readonly Atom _containingAtom;
        private readonly IAtomPlugin _plugin;
        private bool _receiving = false;
        private int _sending = 0;

        public PeerManager(Atom containingAtom, IAtomPlugin plugin)
        {
            _containingAtom = containingAtom;
            _plugin = plugin;
        }

        #region Unity integration

        public void Ready()
        {
            ScanForAtoms();
            BroadcastToTimelines(nameof(ITimelineListener.OnTimelineAnimationReady));
        }

        public void Unready()
        {
            BroadcastToTimelines(nameof(ITimelineListener.OnTimelineAnimationDisabled));
        }

        public void OnTimelineAnimationReady(JSONStorable storable)
        {
            if (ReferenceEquals(storable, _plugin) || _peers.Contains(storable)) return;
            _peers.Add(storable);
        }

        public void OnTimelineAnimationDisabled(JSONStorable storable)
        {
            _peers.Remove(storable);
        }

        public void OnTimelineEvent(object[] e)
        {
            if (_receiving)
                throw new InvalidOperationException("Already syncing, infinite loop avoided!");

            _receiving = true;
            try
            {
                switch ((string)e[0])
                {
                    case nameof(SendPlaybackState):
                        ReceivePlaybackState(e);
                        break;
                    case nameof(SendMasterClipState):
                        ReceiveMasterClipState(e);
                        break;
                    case nameof(SendTime):
                        ReceiveTime(e);
                        break;
                    case nameof(SendCurrentAnimation):
                        ReceiveCurrentAnimation(e);
                        break;
                    case nameof(SendScreen):
                        ReceiveScreen(e);
                        break;
                    case nameof(SendSyncAnimation):
                        ReceiveSyncAnimation(e);
                        break;
                    case nameof(SendStopAndReset):
                        ReceiveStopAndReset(e);
                        break;
                    default:
                        SuperController.LogError($"Received message name {e[0]} but no handler exists for that event");
                        break;
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(PeerManager)}.{nameof(OnTimelineEvent)}: {exc}");
            }
            finally
            {
                _receiving = false;
            }
        }

        private void BroadcastToTimelines(string methodName)
        {
            foreach (var atom in SuperController.singleton.GetAtoms())
            {
                if (atom == _containingAtom) continue;
                var pluginId = atom.GetStorableIDs().FirstOrDefault(id => id.EndsWith("VamTimeline.AtomPlugin"));
                if (pluginId != null)
                {
                    var plugin = atom.GetStorableByID(pluginId);
                    if (ReferenceEquals(plugin, _plugin)) continue;
                    plugin.SendMessage(methodName, _plugin, SendMessageOptions.RequireReceiver);
                }
            }
        }

        private void ScanForAtoms()
        {
            foreach (var atom in SuperController.singleton.GetAtoms())
            {
                if (atom == null) continue;
                var storableId = atom.GetStorableIDs().FirstOrDefault(id => id.EndsWith("VamTimeline.AtomPlugin"));
                if (storableId == null) continue;
                var storable = atom.GetStorableByID(storableId);
                if (ReferenceEquals(storable, _plugin)) continue;
                if (!storable.enabled) continue;
                OnTimelineAnimationReady(storable);
            }
        }

        #endregion

        #region Messages

        public void SendPlaybackState(AtomAnimationClip clip)
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendPlaybackState),
                 clip.animationName,
                 clip.playbackEnabled,
                 clip.clipTime,
                 animation.sequencing,
            });
        }

        private void ReceivePlaybackState(object[] e)
        {
            foreach (var clip in GetClips(e))
            {
                if (clip == null) return;
                if ((bool)e[2])
                    animation.PlayClip(clip, (bool)e[4]);
                else
                    animation.StopClip(clip);
                clip.clipTime = (float)e[3];
            }
        }

        public void SendMasterClipState(AtomAnimationClip clip)
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendMasterClipState), // 0
                 clip.animationName, // 1
                 clip.clipTime, //2
            });
        }

        private void ReceiveMasterClipState(object[] e)
        {
            if (animation.master)
            {
                SuperController.LogError($"Atom {_containingAtom.name} received a master clip state from another atom. Please make sure only one of your atoms is a sequence master during playback.");
                return;
            }
            foreach (var clip in GetClips(e))
            {
                if (clip == null || clip.playbackEnabled) return;
                animation.PlayClip(clip, true);
            }
        }

        public void SendStopAndReset()
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendStopAndReset) // 0
            });
        }

        private void ReceiveStopAndReset(object[] _)
        {
            animation.StopAndReset();
        }

        public void SendTime(AtomAnimationClip clip)
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendTime), // 0
                 clip.animationName, // 1
                 clip.clipTime, // 2
            });
        }

        private void ReceiveTime(object[] e)
        {
            foreach (var clip in GetClips(e))
            {
                if (clip != animationEditContext.current) return;
                animationEditContext.clipTime = (float)e[2];
            }
        }

        public void SendCurrentAnimation(AtomAnimationClip clip)
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendCurrentAnimation),
                 clip.animationName,
            });
        }

        private void ReceiveCurrentAnimation(object[] e)
        {
            foreach (var clip in GetClips(e))
            {
                if (clip == null) return;
                animationEditContext.SelectAnimation(clip);
            }
        }

        public void SendSyncAnimation(AtomAnimationClip clip)
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendSyncAnimation), // 0
                 clip.animationName, // 1
                 clip.animationLayer, // 2
                 clip.animationLength, // 3
                 clip.nextAnimationName, // 4
                 clip.nextAnimationTime, // 5
                 clip.blendInDuration, // 6
                 clip.autoPlay, // 7
                 clip.loop, // 8
                 clip.autoTransitionPrevious, // 9
                 clip.autoTransitionNext, // 10
                 clip.speed, // 11
                 clip.weight, // 12
                 clip.uninterruptible, // 13
                 clip.preserveLoops // 14
            });
        }

        private void ReceiveSyncAnimation(object[] e)
        {
            string animationName = (string)e[1];
            string animationLayer = (string)e[2];

            var existing = animation.GetClip(animationLayer, animationName);
            if (existing == null)
            {
                existing = animation.clips.FirstOrDefault(c => c.animationName == animationName);
                if (existing == null)
                {
                    if (animation.clips.Any(c => c.animationLayer == animationLayer))
                    {
                        existing = new OperationsFactory(_plugin.containingAtom, animation, animation.clips.First(c => c.animationLayer == animationLayer)).AddAnimation().AddAnimationFromCurrentFrame();
                        existing.animationName = animationName;
                    }
                    else
                    {
                        existing = animation.CreateClip(animationLayer, animationName);
                    }
                }
            }

            foreach (var clip in animation.GetClips(animationName))
            {
                new OperationsFactory(_plugin.containingAtom, animation, clip).Resize().CropOrExtendEnd((float)e[3]);
                var nextAnimationName = (string)e[4];
                if (!string.IsNullOrEmpty(nextAnimationName) && animation.index.ByLayer(clip.animationLayer).Any(c => c.animationName == nextAnimationName))
                {
                    clip.nextAnimationName = nextAnimationName;
                    clip.nextAnimationTime = 0f; // Will be managed by the master setting
                    clip.autoTransitionNext = (bool)e[10];
                }
                if (animation.index.ByLayer(clip.animationLayer).Any(c => c.nextAnimationName == clip.animationName))
                {
                    clip.autoTransitionPrevious = (bool)e[9];
                }
                clip.blendInDuration = (float)e[6];
                clip.loop = (bool)e[8];
                clip.speed = (float)e[11];
                clip.weight = (float)e[12];
                clip.uninterruptible = (bool)e[13];
                clip.preserveLoops = (bool)e[14];
                animationEditContext.SelectAnimation(clip);
            }
        }

        public void SendScreen(string screenName, object screenArg)
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendScreen),
                 screenName,
                 screenArg,
            });
        }

        private void ReceiveScreen(object[] e)
        {
            _plugin.ChangeScreen((string)e[1], e[2]);
        }

        private void SendTimelineEvent(object[] e)
        {
            Begin();
            try
            {
                foreach (var storable in _peers)
                {
                    if (storable == null) continue;
                    storable.SendMessage(nameof(OnTimelineEvent), e);
                }
            }
            finally
            {
                Complete();
            }
        }

        private IEnumerable<AtomAnimationClip> GetClips(object[] e)
        {
            return animation.GetClips((string)e[1]);
        }

        public void Begin()
        {
            _sending++;
        }

        public void Complete()
        {
            _sending--;
        }

        #endregion
    }
}
