using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;

namespace ChessApp
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

            if (!Directory.Exists(Path.Combine(FileSystem.AppDataDirectory, "games")))
            {
                Directory.CreateDirectory(Path.Combine(FileSystem.AppDataDirectory, "games"));
            }

            if (!Directory.Exists(Path.Combine(FileSystem.AppDataDirectory, "learning")))
            {
                Directory.CreateDirectory(Path.Combine(FileSystem.AppDataDirectory, "learning"));
            }
        }

        async void start_clicked(object sender, System.EventArgs e)
        {
            bool start_new = true;

            if (File.Exists(Path.Combine(FileSystem.AppDataDirectory, "games", "game_data.txt")))
            {
                start_new = await DisplayAlert("Unfinished game found!", "Would you like to continue the last game?", "No", "Yes");

                if (start_new)
                {
                    // Rename
                    File.Move(Path.Combine(FileSystem.AppDataDirectory, "games", "game_data.txt"), Path.Combine(FileSystem.AppDataDirectory, "games", DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss") + "_data.txt"));
                }
            }

            if (start_new)
            {
                Debug.WriteLine("Starting new game");

                await Navigation.PushAsync(new GameSettings());
            }
            else
            {
                Debug.WriteLine("Continuing old game");

                bool team_white = true;
                int time_limit_white = 300;
                int time_limit_black = 300;
                int time_increment = 5;

                string game_data = File.ReadAllText(Path.Combine(FileSystem.AppDataDirectory, "games", "game_data.txt"));

                string[] separate_data = game_data.Split("\n".ToCharArray());

                if (separate_data.Length > 1)
                {
                    string[] temp_data = separate_data[0].Split("|".ToCharArray());

                    team_white = (temp_data[0] == "w");
                    time_limit_white = int.Parse(temp_data[1]);
                    time_limit_black = int.Parse(temp_data[2]);
                    time_increment = int.Parse(temp_data[3]);

                    game_data = separate_data[1];
                }
                else
                {
                    game_data = separate_data[0];
                }

                await Navigation.PushAsync(new Chessboard(team_white, time_limit_white, time_limit_black, time_increment, game_data));
            }
        }

        async void exit_clicked(object sender, System.EventArgs e)
        {
            bool answer = await DisplayAlert("Exit", "Do you want to exit the game?", "Yes", "No");

            if (answer)
            {
                Debug.WriteLine("Exiting");
            }

        }

        async void learn_clicked(object sender, System.EventArgs e)
        {

            await Navigation.PushAsync(new Trainingsettings());

            Debug.WriteLine("Learning");
        }
    }
}
