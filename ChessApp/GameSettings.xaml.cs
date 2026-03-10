using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ChessApp
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class GameSettings : ContentPage
    {

        private bool team_white = false;
        private int time_limit = 300;
        private int time_increment = 3;

        // Dictionary to get Color from color name.
        Dictionary<string, int> timeToSeconds = new Dictionary<string, int>
        {
            { "1:00", 60}, { "3:00", 180}, { "5:00", 300},
            { "10:00", 600}, { "15:00", 15*60}, { "-", -1}
        };

        public GameSettings()
        {
            InitializeComponent();

            OnSwitchTeam(null, null);

            foreach (string text in timeToSeconds.Keys)
            {
                time_picker.Items.Add(text);
            }

            time_picker.SelectedIndex = 2;

            time_picker.SelectedIndexChanged += (sender, args) =>
            {
                if (time_picker.SelectedIndex == -1)
                {
                    time_limit = 300;
                }
                else
                {
                    string text = time_picker.Items[time_picker.SelectedIndex];
                    time_limit = timeToSeconds[text];
                }
            };

            increment_slider.ValueChanged += (sender, args) =>
            {
                var newStep = Math.Round(args.NewValue / 1.0);
                increment_slider.Value = newStep * 1.0;
                time_increment = (int)increment_slider.Value;
            };

        }

        private void OnSwitchTeam(object sender, EventArgs args)
        {
            team_white = !team_white;

            team_image.Source = team_white ? ImageSource.FromResource("ChessApp.images.white_wk.png", typeof(MainPage).GetTypeInfo().Assembly) : ImageSource.FromResource("ChessApp.images.white_bk.png", typeof(MainPage).GetTypeInfo().Assembly);
        }

        private async void OnGameStart(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new Chessboard(team_white, time_limit, time_increment));
        }
    }
}