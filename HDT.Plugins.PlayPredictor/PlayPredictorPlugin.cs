using System;
using System.Windows.Controls;
using Hearthstone_Deck_Tracker.API;
using Hearthstone_Deck_Tracker.Plugins;

namespace HDT.Plugins.PlayPredictor
{
    public class PlayPredictorPlugin : IPlugin
    {
        private PlayPredictorList _playPredictorList;
        private PlayPredictor _playPredictor;

        public void OnLoad()
        {
            _playPredictorList = new PlayPredictorList();            
            Core.OverlayCanvas.Children.Add(_playPredictorList);
            _playPredictor = new PlayPredictor(_playPredictorList);
            _playPredictor.Init();
        }

        // Triggered when the user unticks the plugin, however, HDT does not completely unload the plugin.
        // see https://git.io/vxEcH
        public void OnUnload()
        {
            Core.OverlayCanvas.Children.Remove(_playPredictorList);
        }

        public void OnButtonPress()
        {
            // Triggered when the user clicks your button in the plugin list
            // TODO: show config menu
        }

        public void OnUpdate()
        {
            // called every ~100ms
            if (!Hearthstone_Deck_Tracker.Core.Game.IsRunning || Hearthstone_Deck_Tracker.Core.Game.IsInMenu)
            {
                _playPredictor.Hide();
            }
        }

        public string Name => "Play Predictor";

        public string Description => "Tries to predict the opponent's next move";

        // Text displayed on the button in "options > tracker > plugins" when your plugin is selected.
        public string ButtonText => "Settings";

        public string Author => "brootski";

        public Version Version => new Version(0, 0, 12);

        // The MenuItem added to the "Plugins" main menu. Return null to not add one.
        public MenuItem MenuItem => null;
    }
}