using UnityEngine;

namespace VamTimeline
{
    public class MoreScreen : ScreenBase
    {
        public const string ScreenName = "More...";

        public override string screenId => ScreenName;


        public MoreScreen()
            : base()
        {
        }

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            prefabFactory.CreateHeader("More options", 1);

            CreateChangeScreenButton("<b>Import / export</b> animations...", ImportExportScreen.ScreenName);
            CreateChangeScreenButton("<b>Bulk</b> changes...", BulkScreen.ScreenName);
            CreateChangeScreenButton("<b>Mocap</b> import...", MocapScreen.ScreenName);
            CreateChangeScreenButton("<b>Advanced</b> keyframe tools...", AdvancedKeyframeToolsScreen.ScreenName);

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("Options...", OptionsScreen.ScreenName);

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("Help", HelpScreen.ScreenName);

            prefabFactory.CreateSpacer();

            var helpButton = prefabFactory.CreateButton("[Browser] Online help");
            helpButton.button.onClick.AddListener(() => Application.OpenURL("https://github.com/acidbubbles/vam-timeline/wiki"));

            var hubButton = prefabFactory.CreateButton("[Browser] Virt-A-Mate Hub");
            hubButton.button.onClick.AddListener(() => Application.OpenURL("https://hub.virtamate.com/resources/timeline.94/"));

            prefabFactory.CreateSpacer();

            var patreonBtn = prefabFactory.CreateButton("[Browser] Support me on Patreon!");
            patreonBtn.textColor = new Color(0.97647f, 0.40784f, 0.32941f);
            patreonBtn.buttonColor = Color.white;
            patreonBtn.button.onClick.AddListener(() => Application.OpenURL("https://www.patreon.com/acidbubbles"));
        }
    }
}

