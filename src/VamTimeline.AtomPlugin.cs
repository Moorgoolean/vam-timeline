using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomPlugin : MVRScript, IAtomPlugin
    {
        private static readonly HashSet<string> GrabbingControllers = new HashSet<string> { "RightHandAnchor", "LeftHandAnchor", "MouseGrab", "SelectionHandles" };

        // Storables

        private const int MaxUndo = 20;
        private const string AllTargets = "(All)";
        private bool _saveEnabled;

        // State
        public AtomAnimation Animation { get; private set; }
        private AtomAnimationSerializer _serializer;
        private bool _restoring;
        private readonly List<string> _undoList = new List<string>();
        private AtomClipboardEntry _clipboard;
        private FreeControllerAnimationTarget _grabbedController;

        // Save
        private JSONStorableString _saveJSON;

        // Storables
        public JSONStorableStringChooser AnimationJSON { get; private set; }
        public JSONStorableAction AddAnimationJSON { get; private set; }
        public JSONStorableFloat ScrubberJSON { get; private set; }
        public JSONStorableAction PlayJSON { get; private set; }
        public JSONStorableAction PlayIfNotPlayingJSON { get; private set; }
        public JSONStorableAction StopJSON { get; private set; }
        public JSONStorableStringChooser FilterAnimationTargetJSON { get; private set; }
        public JSONStorableAction NextFrameJSON { get; private set; }
        public JSONStorableAction PreviousFrameJSON { get; private set; }
        public JSONStorableAction SmoothAllFramesJSON { get; private set; }
        public JSONStorableAction CutJSON { get; private set; }
        public JSONStorableAction CopyJSON { get; private set; }
        public JSONStorableAction PasteJSON { get; private set; }
        public JSONStorableAction UndoJSON { get; private set; }
        public JSONStorableBool LockedJSON { get; private set; }
        public JSONStorableFloat LengthJSON { get; private set; }
        public JSONStorableFloat SpeedJSON { get; private set; }
        public JSONStorableFloat BlendDurationJSON { get; private set; }
        public JSONStorableStringChooser DisplayModeJSON { get; private set; }
        public JSONStorableString DisplayJSON { get; private set; }
        public JSONStorableStringChooser ChangeCurveJSON { get; private set; }

        public JSONStorableStringChooser AddControllerListJSON { get; private set; }
        public JSONStorableAction ToggleControllerJSON { get; private set; }
        public JSONStorableStringChooser LinkedAnimationPatternJSON { get; private set; }

        private class FloatParamJSONRef
        {
            public JSONStorable Storable;
            public JSONStorableFloat SourceFloatParam;
            public JSONStorableFloat Proxy;
            public UIDynamicSlider Slider;
        }

        private List<FloatParamJSONRef> _jsfJSONRefs;

        // Storables
        private JSONStorableStringChooser _addStorableListJSON;
        private JSONStorableStringChooser _addParamListJSON;

        // UI
        private UIDynamicButton _toggleFloatParamUI;
        private AtomAnimationUIManager _ui;

        #region Init

        public override void Init()
        {
            try
            {
                _serializer = new AtomAnimationSerializer(containingAtom);
                _ui = new AtomAnimationUIManager(this);
                InitStorables();
                InitFloatParamsStorables();
                InitFloatParamsCustomUI();
                // Try loading from backup
                StartCoroutine(CreateAnimationIfNoneIsLoaded());
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.Init: " + exc);
            }
        }

        #endregion

        #region Update

        public void Update()
        {
            try
            {
                if (LockedJSON == null || LockedJSON.val || Animation == null) return;

                if (Animation.IsPlaying())
                {
                    var time = Animation.Time;
                    if (time != ScrubberJSON.val)
                        ScrubberJSON.valNoCallback = time;
                    UpdatePlaying();
                    // RenderState() // In practice, we don't see anything useful
                }
                else
                {
                    UpdateNotPlaying();
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.Update: " + exc);
            }
        }

        protected void UpdatePlaying()
        {
            Animation.Update();

            if (!LockedJSON.val)
                ContextUpdatedCustom();
        }

        protected void UpdateNotPlaying()
        {
            var grabbing = SuperController.singleton.RightGrabbedController ?? SuperController.singleton.LeftGrabbedController;
            if (grabbing != null && grabbing.containingAtom != containingAtom)
                grabbing = null;
            else if (Input.GetMouseButton(0) && grabbing == null)
                grabbing = containingAtom.freeControllers.FirstOrDefault(c => GrabbingControllers.Contains(c.linkToRB?.gameObject.name));

            if (_grabbedController == null && grabbing != null)
            {
                _grabbedController = Animation.Current.TargetControllers.FirstOrDefault(c => c.Controller == grabbing);
                AddControllerListJSON.val = grabbing.name;
            }
            else if (_grabbedController != null && grabbing == null)
            {
                // TODO: This should be done by the controller (updating the animation resets the time)
                var time = Animation.Time;
                _grabbedController.SetKeyframeToCurrentTransform(time);
                Animation.RebuildAnimation();
                UpdateTime(time);
                _grabbedController = null;
                AnimationUpdated();
            }
        }

        #endregion

        #region Lifecycle

        public void OnEnable()
        {
            try
            {
                // TODO
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.OnEnable: " + exc);
            }
        }

        public void OnDisable()
        {
            try
            {
                Animation?.Stop();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.OnDisable: " + exc);
            }
        }

        public void OnDestroy()
        {
            OnDisable();
        }

        #endregion


        #region Initialization

        public void InitCommonStorables()
        {
            _saveJSON = new JSONStorableString(StorableNames.Save, "", (string v) => RestoreState(v));
            RegisterString(_saveJSON);

            AnimationJSON = new JSONStorableStringChooser(StorableNames.Animation, new List<string>(), "Anim1", "Animation", val => ChangeAnimation(val))
            {
                isStorable = false
            };
            RegisterStringChooser(AnimationJSON);

            AddAnimationJSON = new JSONStorableAction(StorableNames.AddAnimation, () => AddAnimation());
            RegisterAction(AddAnimationJSON);

            ScrubberJSON = new JSONStorableFloat(StorableNames.Time, 0f, v => UpdateTime(v), 0f, 5f - float.Epsilon, true)
            {
                isStorable = false
            };
            RegisterFloat(ScrubberJSON);

            PlayJSON = new JSONStorableAction(StorableNames.Play, () => { Animation.Play(); ContextUpdated(); });
            RegisterAction(PlayJSON);

            PlayIfNotPlayingJSON = new JSONStorableAction(StorableNames.PlayIfNotPlaying, () => { if (!Animation.IsPlaying()) { Animation.Play(); ContextUpdated(); } });
            RegisterAction(PlayIfNotPlayingJSON);

            StopJSON = new JSONStorableAction(StorableNames.Stop, () => { Animation.Stop(); ContextUpdated(); });
            RegisterAction(StopJSON);

            FilterAnimationTargetJSON = new JSONStorableStringChooser(StorableNames.FilterAnimationTarget, new List<string> { AllTargets }, AllTargets, StorableNames.FilterAnimationTarget, val => { Animation.Current.SelectTargetByName(val == AllTargets ? "" : val); ContextUpdated(); })
            {
                isStorable = false
            };
            RegisterStringChooser(FilterAnimationTargetJSON);

            NextFrameJSON = new JSONStorableAction(StorableNames.NextFrame, () => { UpdateTime(Animation.Current.GetNextFrame(Animation.Time)); ContextUpdated(); });
            RegisterAction(NextFrameJSON);

            PreviousFrameJSON = new JSONStorableAction(StorableNames.PreviousFrame, () => { UpdateTime(Animation.Current.GetPreviousFrame(Animation.Time)); ContextUpdated(); });
            RegisterAction(PreviousFrameJSON);

            SmoothAllFramesJSON = new JSONStorableAction(StorableNames.SmoothAllFrames, () => SmoothAllFrames());

            CutJSON = new JSONStorableAction("Cut", () => Cut());
            CopyJSON = new JSONStorableAction("Copy", () => Copy());
            PasteJSON = new JSONStorableAction("Paste", () => Paste());
            UndoJSON = new JSONStorableAction("Undo", () => Undo());

            LockedJSON = new JSONStorableBool(StorableNames.Locked, false, (bool val) => AnimationUpdated());
            RegisterBool(LockedJSON);

            LengthJSON = new JSONStorableFloat(StorableNames.AnimationLength, 5f, v => UpdateAnimationLength(v), 0.5f, 120f, false, true);

            SpeedJSON = new JSONStorableFloat(StorableNames.AnimationSpeed, 1f, v => UpdateAnimationSpeed(v), 0.001f, 5f, false);

            BlendDurationJSON = new JSONStorableFloat(StorableNames.BlendDuration, 1f, v => UpdateBlendDuration(v), 0.001f, 5f, false);

            DisplayModeJSON = new JSONStorableStringChooser(StorableNames.DisplayMode, RenderingModes.Values, RenderingModes.Default, "Display Mode", (string val) => { ContextUpdated(); });
            DisplayJSON = new JSONStorableString(StorableNames.Display, "")
            {
                isStorable = false
            };
            RegisterString(DisplayJSON);
        }

        protected IEnumerator CreateAnimationIfNoneIsLoaded()
        {
            if (Animation != null)
            {
                _saveEnabled = true;
                yield break;
            }
            yield return new WaitForEndOfFrame();
            try
            {
                RestoreState(_saveJSON.val);
            }
            finally
            {
                _saveEnabled = true;
            }
        }

        #endregion

        #region Load / Save

        public void RestoreState(string json)
        {
            if (_restoring) return;
            _restoring = true;

            try
            {
                if (Animation != null)
                    Animation = null;

                if (!string.IsNullOrEmpty(json))
                {
                    Animation = _serializer.DeserializeAnimation(json);
                }

                if (Animation == null)
                {
                    var backupStorableID = containingAtom.GetStorableIDs().FirstOrDefault(s => s.EndsWith("VamTimeline.BackupPlugin"));
                    if (backupStorableID != null)
                    {
                        var backupStorable = containingAtom.GetStorableByID(backupStorableID);
                        var backupJSON = backupStorable.GetStringJSONParam(StorableNames.AtomAnimationBackup);
                        if (backupJSON != null && !string.IsNullOrEmpty(backupJSON.val))
                        {
                            SuperController.LogMessage("No save found but a backup was detected. Loading backup.");
                            Animation = _serializer.DeserializeAnimation(backupJSON.val);
                        }
                    }
                }

            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.PluginImplBase.RestoreState(1): " + exc);
            }

            try
            {
                if (Animation == null)
                    Animation = _serializer.CreateDefaultAnimation();

                Animation.Initialize();
                StateRestored();
                AnimationUpdated();
                ContextUpdated();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.PluginImplBase.RestoreState(2): " + exc);
            }

            _restoring = false;
        }

        public void SaveState()
        {
            try
            {
                if (_restoring) return;
                if (Animation.IsEmpty()) return;

                var serialized = _serializer.SerializeAnimation(Animation);

                if (serialized == _undoList.LastOrDefault())
                    return;

                if (!string.IsNullOrEmpty(_saveJSON.val))
                {
                    _undoList.Add(_saveJSON.val);
                    if (_undoList.Count > MaxUndo) _undoList.RemoveAt(0);
                    // _undoUI.button.interactable = true;
                }

                _saveJSON.valNoCallback = serialized;

                var backupStorableID = containingAtom.GetStorableIDs().FirstOrDefault(s => s.EndsWith("VamTimeline.BackupPlugin"));
                if (backupStorableID != null)
                {
                    var backupStorable = containingAtom.GetStorableByID(backupStorableID);
                    var backupJSON = backupStorable.GetStringJSONParam(StorableNames.AtomAnimationBackup);
                    if (backupJSON != null)
                        backupJSON.val = serialized;
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.PluginImplBase.SaveState: " + exc);
            }
        }

        #endregion

        #region Callbacks

        private void ChangeAnimation(string animationName)
        {
            try
            {
                FilterAnimationTargetJSON.val = AllTargets;
                Animation.ChangeAnimation(animationName);
                AnimationJSON.valNoCallback = animationName;
                SpeedJSON.valNoCallback = Animation.Speed;
                LengthJSON.valNoCallback = Animation.AnimationLength;
                ScrubberJSON.max = Animation.AnimationLength - float.Epsilon;
                AnimationUpdated();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.ChangeAnimation: " + exc);
            }
        }

        protected void UpdateTime(float time)
        {
            Animation.Time = time;
            if (Animation.Current.AnimationPattern != null)
                Animation.Current.AnimationPattern.SetFloatParamValue("currentTime", time);
            ContextUpdated();
        }

        private void UpdateAnimationLength(float v)
        {
            if (v <= 0) return;
            Animation.AnimationLength = v;
            AnimationUpdated();
        }

        private void UpdateAnimationSpeed(float v)
        {
            if (v < 0) return;
            Animation.Speed = v;
            AnimationUpdated();
        }

        private void UpdateBlendDuration(float v)
        {
            if (v < 0) return;
            Animation.BlendDuration = v;
            AnimationUpdated();
        }

        private void Cut()
        {
            Copy();
            if (Animation.Time == 0f) return;
            Animation.DeleteFrame();
        }

        private void Copy()
        {
            try
            {
                _clipboard = Animation.Copy();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.Copy: " + exc);
            }
        }

        private void Paste()
        {
            try
            {
                if (_clipboard == null)
                {
                    SuperController.LogMessage("Clipboard is empty");
                    return;
                }
                var time = Animation.Time;
                Animation.Paste(_clipboard);
                // Sample animation now
                UpdateTime(time);
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.Paste: " + exc);
            }
        }

        private void Undo()
        {
            if (_undoList.Count == 0) return;
            var animationName = AnimationJSON.val;
            var pop = _undoList[_undoList.Count - 1];
            _undoList.RemoveAt(_undoList.Count - 1);
            // TODO: Removed while extracting UI
            // if (_undoList.Count == 0) _undoUI.button.interactable = false;
            if (string.IsNullOrEmpty(pop)) return;
            var time = Animation.Time;
            _saveEnabled = false;
            try
            {
                RestoreState(pop);
                _saveJSON.valNoCallback = pop;
                if (Animation.Clips.Any(c => c.AnimationName == animationName))
                    AnimationJSON.val = animationName;
                else
                    AnimationJSON.valNoCallback = Animation.Clips.First().AnimationName;
                AnimationUpdated();
                UpdateTime(time);
            }
            finally
            {
                _saveEnabled = true;
            }
        }

        private void AddAnimation()
        {
            _saveEnabled = false;
            try
            {
                var animationName = Animation.AddAnimation();
                AnimationUpdated();
                ChangeAnimation(animationName);
            }
            finally
            {
                _saveEnabled = true;
            }
            SaveState();
        }

        private void ChangeCurve(string curveType)
        {
            if (string.IsNullOrEmpty(curveType)) return;
            ChangeCurveJSON.valNoCallback = "";
            if (Animation.Time == 0)
            {
                SuperController.LogMessage("Cannot specify curve type on frame 0");
                return;
            }
            Animation.ChangeCurve(curveType);
        }

        private void SmoothAllFrames()
        {
            Animation.SmoothAllFrames();
        }

        private void ToggleAnimatedController()
        {
            try
            {
                var uid = AddControllerListJSON.val;
                var controller = containingAtom.freeControllers.Where(x => x.name == uid).FirstOrDefault();
                if (controller == null)
                {
                    SuperController.LogError($"Controller {uid} in atom {containingAtom.uid} does not exist");
                    return;
                }
                if (Animation.Current.TargetControllers.Any(c => c.Controller == controller))
                {
                    Animation.Remove(controller);
                }
                else
                {
                    controller.currentPositionState = FreeControllerV3.PositionState.On;
                    controller.currentRotationState = FreeControllerV3.RotationState.On;
                    var animController = Animation.Add(controller);
                    animController.SetKeyframeToCurrentTransform(0f);
                }
                AnimationUpdated();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.AddSelectedController: " + exc);
            }
        }

        private void LinkAnimationPattern(string uid)
        {
            if (string.IsNullOrEmpty(uid))
            {
                Animation.Current.AnimationPattern = null;
                return;
            }
            var animationPattern = SuperController.singleton.GetAtomByUid(uid)?.GetComponentInChildren<AnimationPattern>();
            if (animationPattern == null)
            {
                SuperController.LogError($"Could not find Animation Pattern '{uid}'");
                return;
            }
            Animation.Current.AnimationPattern = animationPattern;
            animationPattern.SetBoolParamValue("autoPlay", false);
            animationPattern.SetBoolParamValue("pause", false);
            animationPattern.SetBoolParamValue("loop", false);
            animationPattern.SetBoolParamValue("loopOnce", false);
            animationPattern.SetFloatParamValue("speed", Animation.Speed);
            animationPattern.ResetAnimation();
            AnimationUpdated();
        }

        #endregion

        #region State Rendering

        public class RenderingModes
        {
            public const string None = "None";
            public const string Default = "Default";
            public const string ShowAllTargets = "Show All Targets";
            public const string Debug = "Debug";

            public static readonly List<string> Values = new List<string> { None, Default, ShowAllTargets, Debug };
        }

        public void RenderState()
        {
            if (LockedJSON.val)
            {
                DisplayJSON.val = "Locked";
                return;
            }

            var time = Animation.Time;

            switch (DisplayModeJSON.val)
            {
                case RenderingModes.None:
                    DisplayJSON.val = "";
                    break;
                case RenderingModes.Default:
                    RenderStateDefault();
                    break;
                case RenderingModes.ShowAllTargets:
                    RenderStateShowAllTargets();
                    break;
                case RenderingModes.Debug:
                    RenderStateDebug();
                    break;
                default:
                    throw new NotSupportedException($"Unknown rendering mode {DisplayModeJSON.val}");
            }
        }

        public void RenderStateDefault()
        {
            var time = ScrubberJSON.val;
            var frames = new List<float>();
            var targets = new List<string>();
            foreach (var target in Animation.Current.GetAllOrSelectedTargets())
            {
                var keyTimes = target.GetAllKeyframesTime();
                foreach (var keyTime in keyTimes)
                {
                    frames.Add(keyTime);
                    if (keyTime == time)
                        targets.Add(target.Name);
                }
            }
            var display = new StringBuilder();
            frames.Sort();
            display.Append("Frames:");
            foreach (var f in frames.Distinct())
            {
                if (f == time)
                    display.Append($"[{f:0.00}]");
                else
                    display.Append($" {f:0.00} ");
            }
            display.AppendLine();
            display.AppendLine("Affects:");
            foreach (var c in targets)
                display.AppendLine(c);
            DisplayJSON.val = display.ToString();
        }

        public void RenderStateShowAllTargets()
        {
            var time = ScrubberJSON.val;
            var display = new StringBuilder();
            foreach (var controller in Animation.Current.GetAllOrSelectedTargets())
            {
                display.AppendLine(controller.Name);
                var keyTimes = controller.GetAllKeyframesTime();
                foreach (var keyTime in keyTimes)
                {
                    display.Append($"{(keyTime == time ? "[" : " ")}{keyTime:0.0000}{(keyTime == time ? "]" : " ")}");
                }
                display.AppendLine();
            }
            DisplayJSON.val = display.ToString();
        }

        public void RenderStateDebug()
        {
            // Instead make a debug screen
            var time = ScrubberJSON.val;
            var display = new StringBuilder();
            display.AppendLine($"Time: {time}s");
            foreach (var controller in Animation.Current.GetAllOrSelectedTargets())
            {
                controller.RenderDebugInfo(display, time);
            }
            DisplayJSON.val = display.ToString();
        }

        #endregion

        #region Updates

        protected void AnimationUpdated()
        {
            try
            {
                // Update UI
                ScrubberJSON.valNoCallback = Animation.Time;
                AnimationJSON.choices = Animation.GetAnimationNames().ToList();
                LengthJSON.valNoCallback = Animation.AnimationLength;
                SpeedJSON.valNoCallback = Animation.Speed;
                LengthJSON.valNoCallback = Animation.AnimationLength;
                BlendDurationJSON.valNoCallback = Animation.BlendDuration;
                ScrubberJSON.max = Animation.AnimationLength - float.Epsilon;
                FilterAnimationTargetJSON.choices = new List<string> { AllTargets }.Concat(Animation.Current.GetTargetsNames()).ToList();

                LinkedAnimationPatternJSON.valNoCallback = Animation.Current.AnimationPattern?.containingAtom.uid ?? "";

                // Save
                if (_saveEnabled)
                    SaveState();

                // Render
                RenderState();

                // UI
                _ui.AnimationUpdated();

                // Dispatch to VamTimelineController
                var externalControllers = SuperController.singleton.GetAtoms().Where(a => a.type == "SimpleSign");
                foreach (var controller in externalControllers)
                    controller.BroadcastMessage("VamTimelineAnimationUpdated", containingAtom.uid);
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.AnimationUpdated: " + exc);
            }
        }

        protected void ContextUpdated()
        {
            try
            {
                var time = Animation.Time;

                // Update UI
                ScrubberJSON.valNoCallback = time;

                ContextUpdatedCustom();

                // Render
                RenderState();

                // Dispatch to VamTimelineController
                var externalControllers = SuperController.singleton.GetAtoms().Where(a => a.type == "SimpleSign");
                foreach (var controller in externalControllers)
                    controller.BroadcastMessage("VamTimelineContextChanged", containingAtom.uid);
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.ContextUpdated: " + exc);
            }
        }

        #endregion

        // Shared
        #region Initialization

        private void InitStorables()
        {
            InitCommonStorables();

            ChangeCurveJSON = new JSONStorableStringChooser(StorableNames.ChangeCurve, CurveTypeValues.CurveTypes, "", "Change Curve", ChangeCurve);

            AddControllerListJSON = new JSONStorableStringChooser("Animate Controller", containingAtom.freeControllers.Select(fc => fc.name).ToList(), containingAtom.freeControllers.Select(fc => fc.name).FirstOrDefault(), "Animate controller", (string name) => _ui.UIUpdated())
            {
                isStorable = false
            };

            ToggleControllerJSON = new JSONStorableAction("Toggle Controller", () => ToggleAnimatedController());

            LinkedAnimationPatternJSON = new JSONStorableStringChooser("Linked Animation Pattern", new[] { "" }.Concat(SuperController.singleton.GetAtoms().Where(a => a.type == "AnimationPattern").Select(a => a.uid)).ToList(), "", "Linked Animation Pattern", (string uid) => LinkAnimationPattern(uid))
            {
                isStorable = false
            };
        }


        #endregion


        #region Initialization

        private void InitFloatParamsStorables()
        {
            InitCommonStorables();

            var storables = GetInterestingStorableIDs().ToList();
            _addStorableListJSON = new JSONStorableStringChooser("Animate Storable", storables, storables.Contains("geometry") ? "geometry" : storables.FirstOrDefault(), "Animate Storable", (string name) => RefreshStorableFloatsList())
            {
                isStorable = false
            };

            _addParamListJSON = new JSONStorableStringChooser("Animate Param", new List<string>(), "", "Animate Param", (string name) => _ui.UIUpdated())
            {
                isStorable = false
            };

            RefreshStorableFloatsList();
        }

        private IEnumerable<string> GetInterestingStorableIDs()
        {
            foreach (var storableId in containingAtom.GetStorableIDs())
            {
                var storable = containingAtom.GetStorableByID(storableId);
                if (storable.GetFloatParamNames().Count > 0)
                    yield return storableId;
            }
        }

        private void RefreshStorableFloatsList()
        {
            if (string.IsNullOrEmpty(_addStorableListJSON.val))
            {
                _addParamListJSON.choices = new List<string>();
                _addParamListJSON.val = "";
                return;
            }
            var values = containingAtom.GetStorableByID(_addStorableListJSON.val)?.GetFloatParamNames() ?? new List<string>();
            _addParamListJSON.choices = values;
            if (!values.Contains(_addParamListJSON.val))
                _addParamListJSON.val = values.FirstOrDefault();
        }

        private void InitFloatParamsCustomUI()
        {
            var addFloatParamListUI = CreateScrollablePopup(_addStorableListJSON, true);
            addFloatParamListUI.popupPanelHeight = 800f;
            addFloatParamListUI.popup.onOpenPopupHandlers += () => _addStorableListJSON.choices = GetInterestingStorableIDs().ToList();

            var addParamListUI = CreateScrollablePopup(_addParamListJSON, true);
            addParamListUI.popupPanelHeight = 700f;
            addParamListUI.popup.onOpenPopupHandlers += () => RefreshStorableFloatsList();

            _toggleFloatParamUI = CreateButton("Add/Remove Param", true);
            _toggleFloatParamUI.button.onClick.AddListener(() => ToggleAnimatedFloatParam());

            RefreshFloatParamsListUI();
        }

        private void RefreshFloatParamsListUI()
        {
            if (_jsfJSONRefs != null)
            {
                foreach (var jsfJSONRef in _jsfJSONRefs)
                {
                    RemoveSlider(jsfJSONRef.Slider);
                }
            }
            if (Animation == null) return;
            // TODO: This is expensive, though rarely occuring
            _jsfJSONRefs = new List<FloatParamJSONRef>();
            foreach (var target in Animation.Current.TargetFloatParams)
            {
                var jsfJSONRef = target.FloatParam;
                var jsfJSONProxy = new JSONStorableFloat($"{target.Storable.name}/{jsfJSONRef.name}", jsfJSONRef.defaultVal, (float val) => UpdateFloatParam(target, jsfJSONRef, val), jsfJSONRef.min, jsfJSONRef.max, jsfJSONRef.constrained, true);
                var slider = CreateSlider(jsfJSONProxy, true);
                _jsfJSONRefs.Add(new FloatParamJSONRef
                {
                    Storable = target.Storable,
                    SourceFloatParam = jsfJSONRef,
                    Proxy = jsfJSONProxy,
                    Slider = slider
                });
            }
        }

        #endregion

        #region Callbacks

        private void ToggleAnimatedFloatParam()
        {
            try
            {
                var storable = containingAtom.GetStorableByID(_addStorableListJSON.val);
                if (storable == null)
                {
                    SuperController.LogError($"Storable {_addStorableListJSON.val} in atom {containingAtom.uid} does not exist");
                    return;
                }
                var sourceFloatParam = storable.GetFloatJSONParam(_addParamListJSON.val);
                if (sourceFloatParam == null)
                {
                    SuperController.LogError($"Param {_addParamListJSON.val} in atom {containingAtom.uid} does not exist");
                    return;
                }
                if (Animation.Current.TargetFloatParams.Any(c => c.FloatParam == sourceFloatParam))
                {
                    Animation.Current.TargetFloatParams.Remove(Animation.Current.TargetFloatParams.First(c => c.FloatParam == sourceFloatParam));
                }
                else
                {
                    var target = new FloatParamAnimationTarget(storable, sourceFloatParam, Animation.AnimationLength);
                    target.SetKeyframe(0, sourceFloatParam.val);
                    Animation.Current.TargetFloatParams.Add(target);
                }
                RefreshFloatParamsListUI();
                AnimationUpdated();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.FloatParamsPlugin.ToggleAnimatedFloatParam: " + exc);
            }
        }

        private void UpdateFloatParam(FloatParamAnimationTarget target, JSONStorableFloat sourceFloatParam, float val)
        {
            sourceFloatParam.val = val;
            // TODO: This should be done by the controller (updating the animation resets the time)
            var time = Animation.Time;
            target.SetKeyframe(time, val);
            Animation.RebuildAnimation();
            UpdateTime(time);
            AnimationUpdated();
        }

        #endregion

        #region Updates

        protected void StateRestored()
        {
            _ui.RefreshCurrentUI();
            RefreshFloatParamsListUI();
        }

        protected void ContextUpdatedCustom()
        {
            if (_jsfJSONRefs != null)
            {
                foreach (var jsfJSONRef in _jsfJSONRefs)
                    jsfJSONRef.Proxy.valNoCallback = jsfJSONRef.SourceFloatParam.val;
            }
        }

        #endregion
    }
}

