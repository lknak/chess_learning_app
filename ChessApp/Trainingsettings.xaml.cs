using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ChessApp
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class Trainingsettings : ContentPage
    {
        private bool team_white = false;
        private string db_path = "db_2500_d40.txt";
        private float time_increment = 1.5f;

        // Dictionary to get Color from color name.
        Dictionary<string, string> dbs = new Dictionary<string, string>
        {
            { ">5000 Games Played", "db_5000_d40.txt"}, { ">2500 Games Played", "db_2500_d40.txt"}, { ">1000 Games Played", "db_1000_d40.txt"}
        };

        public Trainingsettings()
        {
            InitializeComponent();

            OnSwitchTeam(null, null);

            foreach (string text in dbs.Keys)
            {
                db_picker.Items.Add(text);
            }

            db_picker.SelectedIndex = 1;

            db_picker.SelectedIndexChanged += (sender, args) =>
            {
                if (db_picker.SelectedIndex == -1)
                {
                    db_path = "db_2500_d40.txt";
                }
                else
                {
                    string text = db_picker.Items[db_picker.SelectedIndex];
                    db_path = dbs[text];
                }
            };

            increment_slider.ValueChanged += (sender, args) =>
            {
                var newStep = Math.Round(args.NewValue * 10.0 / 1.0) / 10.0;
                increment_slider.Value = newStep * 1.0;
                time_increment = (int)increment_slider.Value;
            };
        }

        private void OnSwitchTeam(object sender, EventArgs args)
        {
            team_white = !team_white;

            team_image.Source = team_white ? ImageSource.FromResource("ChessApp.images.white_wk.png", typeof(MainPage).GetTypeInfo().Assembly) : ImageSource.FromResource("ChessApp.images.white_bk.png", typeof(MainPage).GetTypeInfo().Assembly);
        }

        private async void OnAllLines(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new Trainingboard(team_white, db_path, is_pruning.IsToggled, is_repetition.IsToggled, true, time_increment));
        }

        private async void OnSingleLine(object sender, EventArgs e)
        {
            await Navigation.PushAsync(new Trainingboard(team_white, db_path, is_pruning.IsToggled, is_repetition.IsToggled, false, time_increment));
        }

        private async void OnShowScores(object sender, EventArgs e)
        {

            List<string> files_ = Directory.EnumerateFiles(Path.Combine(FileSystem.AppDataDirectory, "learning")).ToList();
            Dictionary<string, int> high_dict = new Dictionary<string, int>();

            int option_length = files_.Count();

            foreach (string file_ in files_)
            {
                if (file_.Contains("highscore"))
                {
                    int score = int.Parse(File.ReadAllText(file_));

                    high_dict.Add(file_.Substring(file_.LastIndexOf('/') + 1).Replace(".txt", "").Replace("high", ""), score);

                    option_length--;
                }
            }

            string[] options = new string[option_length];

            int current_line = 0;
            foreach (string file_ in files_)
            {
                int score = int.Parse(File.ReadAllText(file_));

                if (!file_.Contains("highscore"))
                {
                    string[] parts = file_.Substring(file_.LastIndexOf('/') + 1).Replace(".txt", "").Split('_');
                    options[current_line] = parts[2] + "." + parts[1] + "." + parts[0] + ": " +
                                            score.ToString() + (high_dict.ContainsKey(file_.Substring(file_.LastIndexOf('/') + 1).Replace(".txt", "").Substring(11)) && score == high_dict[file_.Substring(file_.LastIndexOf('/') + 1).Replace(".txt", "").Substring(11)] ? " (Highscore)" : "") + "\n" +
                                            "(DB " + parts[5] + "-" + parts[6] +
                                            " " + (parts[8] == "a" ? "All Lines" : "Single Lines") +
                                            " " + (parts[7] == "w" ? "White" : "Black") + ")";
                    current_line++;
                }
            }

            SelectionPage sp = new SelectionPage("Scores", "", "Close", options);

            while (true)
            {
                string action = await sp.Select();

                if (action == null)
                {
                    break;
                }
                else if (action == "Close")
                {
                    // close 
                    await sp.Close();
                    break;
                }
            }
        }
    }
}