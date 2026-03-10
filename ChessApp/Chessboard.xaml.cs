using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using Xamarin.Essentials;
using Xamarin.Forms;
using Xamarin.Forms.Xaml;

namespace ChessApp
{
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class Chessboard : ContentPage
    {
        private ChessEngine chess_engine;
        private int clicked_pos_ = -1;
        private List<int> pos_to_change = new List<int>();

        private bool team_white;
        private int time_limit_white;
        private int time_limit_black;
        private int time_increment;

        private bool timer_active = false;
        private bool do_save = true;

        public Chessboard(bool team_white, int time_limit, int time_increment)
        {
            this.team_white = team_white;
            this.time_limit_white = time_limit;
            this.time_limit_black = time_limit;
            this.time_increment = time_increment;

            InitializeComponent();

            if (!team_white)
            {
                label_time_white.Rotation = 180;
                label_time_black.Rotation = 0;
            }

            Field[] fields = initializeBoard();

            chess_engine = new ChessEngine(this.OnClick, fields);

            updateBoard();

            SetTimerLabels();
        }

        public Chessboard(bool team_white, int time_limit_white, int time_limit_black, int time_increment, string board_string_)
        {
            this.team_white = team_white;
            this.time_limit_white = time_limit_white;
            this.time_limit_black = time_limit_black;
            this.time_increment = time_increment;

            InitializeComponent();

            if (!team_white)
            {
                label_time_white.Rotation = 180;
                label_time_black.Rotation = 0;
            }

            Field[] fields = initializeBoard();

            chess_engine = new ChessEngine(this.OnClick, fields, board_string_);

            updateBoard();

            SetTimerLabels();
        }

        private void SetTimerLabels()
        {

            if (time_limit_white > -1 && time_limit_black > -1)
            {
                label_time_white.Text = (time_limit_white / 60).ToString().PadLeft(2, '0') + ":" + (time_limit_white % 60).ToString().PadLeft(2, '0');
                label_time_black.Text = (time_limit_black / 60).ToString().PadLeft(2, '0') + ":" + (time_limit_black % 60).ToString().PadLeft(2, '0');
            }
        }

        private void ActivateTimer()
        {

            Device.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                if (chess_engine.GetGamestate() != 2)
                {
                    if (chess_engine.GetWhiteToMove())
                    {
                        time_limit_white--;

                        if (time_limit_white == 0)
                        {
                            label_gamestate.Text = "White is out of time!";
                            return false;
                        }
                    }
                    else
                    {
                        time_limit_black--;

                        if (time_limit_black == 0)
                        {
                            label_gamestate.Text = "Black is out of time!";
                            return false;
                        }
                    }

                    SetTimerLabels();
                }

                return true;
            });
        }

        protected override void OnParentSet()
        {
            base.OnParentSet();

            Debug.WriteLine("Closing page, saving?");

            SaveData();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            if (do_save) Debug.WriteLine("Navigating page, saving?");

            if (do_save) SaveData();
        }

        private void SaveData()
        {
            string game_data = chess_engine.GetPGN();

            if (game_data.Length > 0)
            {
                // Append time info
                game_data = (team_white ? "w" : "b") + "|" + time_limit_white + "|" + time_limit_black + "|" + time_increment + "\n" + game_data;
                File.WriteAllText(Path.Combine(FileSystem.AppDataDirectory, "games", "game_data.txt"), game_data);
            }

        }

        private async void OnClick(int position_)
        {

            if (clicked_pos_ != -1 && pos_to_change.Contains(position_))
            {
                // Promotion check
                string action = "";
                if ((position_ / 8 == 0 || position_ / 8 == 7) && chess_engine.GetPiece(clicked_pos_) is PiecePawn)
                {
                    do_save = false;
                    action = await SelectionPage.Choose("Promote to", "", "Cancel", "Queen", "Rook", "Bishop", "Knight");
                    do_save = true;

                    if (action == null || action == "Cancel")
                    {
                        return;
                    }
                }

                List<int> temp_to_change = chess_engine.MovePiece(clicked_pos_, position_, action);

                // Change temp images
                foreach (int pos in temp_to_change)
                {
                    updateImage(pos, chess_engine.chess_field[pos].GetImage());
                }

                // Update clicked pos first
                if (clicked_pos_ != -1) updateImage(clicked_pos_, chess_engine.chess_field[clicked_pos_].GetImage());

                // Clear stuff from before
                foreach (int pos_ in pos_to_change)
                {
                    updateImage(pos_, chess_engine.chess_field[pos_].GetImage());
                }

                pos_to_change.Clear();
                clicked_pos_ = -1;

                updateLabels();

                if (!timer_active)
                {
                    ActivateTimer();
                    timer_active = true;
                }
                else
                {
                    // Increment time
                    if (!chess_engine.GetWhiteToMove())
                    {
                        time_limit_white += time_increment;
                    }
                    else
                    {
                        time_limit_black += time_increment;
                    }

                    SetTimerLabels();
                }
            }
            else
            {
                // Clear stuff before
                foreach (int pos_ in pos_to_change)
                {
                    updateImage(pos_, chess_engine.chess_field[pos_].GetImage());
                }

                pos_to_change.Clear();

                // Image activated piece change
                if (clicked_pos_ != -1) updateImage(clicked_pos_, chess_engine.chess_field[clicked_pos_].GetImage());

                if (clicked_pos_ != position_)
                {
                    clicked_pos_ = -1;

                    // Calculate new things directly
                    Piece p = chess_engine.GetPiece(position_);

                    if (p != null && p.getTeamWhite() == chess_engine.GetWhiteToMove())
                    {
                        pos_to_change = p.getMoves(chess_engine);

                        foreach (int pos_ in pos_to_change)
                        {
                            updateImage(pos_, chess_engine.chess_field[pos_].GetImageTarget());
                        }

                        // Image activated piece change
                        updateImage(position_, chess_engine.chess_field[position_].GetImageSelected());

                        clicked_pos_ = position_;
                    }
                }
                else
                {
                    clicked_pos_ = -1;
                }
            }
        }

        private void reverseMove(object sender, EventArgs args)
        {
            List<int> temp_to_change = chess_engine.ReverseMove();

            // Change temp images
            foreach (int pos in temp_to_change)
            {
                updateImage(pos, chess_engine.chess_field[pos].GetImage());
            }

            foreach (int pos_ in pos_to_change)
            {
                updateImage(pos_, chess_engine.chess_field[pos_].GetImage());
            }

            clicked_pos_ = -1;
            pos_to_change.Clear();

            updateLabels();
        }

        private void updateLabels()
        {
            // Update FEN, PGN and state
            string fen_string = chess_engine.GetFEN();
            string pgn_string = chess_engine.GetPGN();

            label_fen.Text = fen_string.Length > 40 ? "..." + fen_string.Substring(fen_string.Length - 37, 37) : fen_string;
            label_moves.Text = pgn_string.Length > 40 ? "..." + pgn_string.Substring(pgn_string.Length - 37, 37) : pgn_string;
            label_gamestate.Text = chess_engine.GetGamestate() == 1 ? "Check!" : (chess_engine.GetGamestate() == 2 ? "Checkmate!" : "");
        }

        private Field[] initializeBoard()
        {
            Field[] fields = new Field[64];

            for (int i = 0; i < 64; i++)
            {
                Image embeddedImage = new Image
                {
                    Source = ImageSource.FromResource("ChessApp.images." + (i % 2 == (i / 8) % 2 ? "white" : "brown") + "_empty.png", typeof(MainPage).GetTypeInfo().Assembly)
                };

                embeddedImage.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command<int>((position_) =>
                    {
                        OnClick(position_);
                    }),
                    CommandParameter = i,
                    NumberOfTapsRequired = 1
                });

                mygrid.Children.Add(embeddedImage);

                Image embeddedImageTarget = new Image
                {
                    Source = ImageSource.FromResource("ChessApp.images." + (i % 2 == (i / 8) % 2 ? "white" : "brown") + "_empty_target.png", typeof(MainPage).GetTypeInfo().Assembly)
                };

                embeddedImageTarget.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command<int>((position_) =>
                    {
                        OnClick(position_);
                    }),
                    CommandParameter = i,
                    NumberOfTapsRequired = 1
                });

                Image embeddedImageMoved = new Image
                {
                    Source = ImageSource.FromResource("ChessApp.images." + (i % 2 == (i / 8) % 2 ? "white" : "brown") + "_empty_moved.png", typeof(MainPage).GetTypeInfo().Assembly)
                };

                embeddedImageMoved.GestureRecognizers.Add(new TapGestureRecognizer
                {
                    Command = new Command<int>((position_) =>
                    {
                        OnClick(position_);
                    }),
                    CommandParameter = i,
                    NumberOfTapsRequired = 1
                });

                fields[i] = new Field(embeddedImage, embeddedImageTarget, embeddedImageMoved);
            }

            return fields;
        }

        private void updateBoard()
        {
            Debug.WriteLine("Updating " + chess_engine.chess_field.Length + " Fields");
            for(int i = 0; i < 64; i++)
            {
                chess_engine.chess_field[i].SetMoved(false);
                updateImage(i, chess_engine.chess_field[i].GetImage());
            }

            updateLabels();
        }

        private void updateBoard(object sender, EventArgs args)
        {
            updateBoard();
        }

        private bool updateImage(int pos_, Image img_)
        {

            Grid.SetColumn(img_, !team_white ? 7 - pos_ % 8 : pos_ % 8);
            Grid.SetRow(img_, !team_white ? 7 - pos_ / 8 : pos_ / 8);

            mygrid.Children.RemoveAt(pos_);
            mygrid.Children.Insert(pos_, img_);
            return true;
        }

        private async void OnCopyFEN(object sender, EventArgs args)
        {
            await Clipboard.SetTextAsync(chess_engine.GetFEN());
        }
        private async void OnCopyPGN(object sender, EventArgs args)
        {
            await Clipboard.SetTextAsync(chess_engine.GetPGN());
        }

    }
}