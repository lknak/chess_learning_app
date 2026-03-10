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

    public partial class Trainingboard : ContentPage
    {
        private ChessEngine chess_engine;
        private int clicked_pos_ = -1;
        private List<int> pos_to_change = new List<int>();

        private bool team_white;
        private bool pruning;
        private bool repetition;
        private bool all_lines;
        private LineMove current_line;
        private string final_line_name = "";
        private float popup_time;

        private List<LineMove> line_ends = new List<LineMove>();
        private List<int> current_line_indices = new List<int>();
        private LineDict line_dict = new LineDict("", null);

        private string db_path = "";
        private float current_score = 0;
        private int high_score = 0;
        private int corrects = 0;
        private int mistakes = 0;
        private bool solution_shown = false;
        private bool do_save = true;

        public Trainingboard(bool team_white, string db_path, bool pruning, bool repetition, bool all_lines) : this(team_white, db_path, pruning, repetition, all_lines, 1.5f) { }

        public Trainingboard(bool team_white, string db_path, bool pruning, bool repetition, bool all_lines, float popup_time)
        {
            this.team_white = team_white;
            this.pruning = pruning;
            this.repetition = repetition;
            this.db_path = db_path;
            this.all_lines = all_lines;
            this.popup_time = popup_time;

            InitializeComponent();

            Field[] fields = initializeBoard();

            // Load scores

            if (File.Exists(Path.Combine(FileSystem.AppDataDirectory, "learning", DateTime.Now.ToString("yyyy_MM_dd_") + "score_" + db_path.Replace(".txt", "") + "_" + (team_white ? "w" : "b") + "_" + (all_lines ? "a" : "s") + ".txt")))
            {
                current_score = int.Parse(File.ReadAllText(Path.Combine(FileSystem.AppDataDirectory, "learning", DateTime.Now.ToString("yyyy_MM_dd_") + "score_" + db_path.Replace(".txt", "") + "_" + (team_white ? "w" : "b") + "_" + (all_lines ? "a" : "s") + ".txt")));
            }

            if (File.Exists(Path.Combine(FileSystem.AppDataDirectory, "learning", "highscore_" + db_path.Replace(".txt", "") + "_" + (team_white ? "w" : "b") + "_" + (all_lines ? "a" : "s") + ".txt")))
            {
                high_score = int.Parse(File.ReadAllText(Path.Combine(FileSystem.AppDataDirectory, "learning", "highscore_" + db_path.Replace(".txt", "") + "_" + (team_white ? "w" : "b") + "_" + (all_lines ? "a" : "s") + ".txt")));
            }

            // Load engine
            chess_engine = new ChessEngine(this.OnClick, fields);

            updateBoard();

            // Load rest
            label_task.TextColor = Color.Red;
            label_task.FontAttributes = FontAttributes.Bold;
            label_task.Text = "Loading DB (0%), please wait ...";

            label_moves.IsVisible = all_lines;

            Task.Run(async () => {
                // Async loading
                int temp_ = LoadDB(db_path);

                // Updating screen afterwards
                Device.BeginInvokeOnMainThread(() =>
                {
                    label_task.FontAttributes = FontAttributes.None;
                    label_task.TextColor = Color.White;

                    if (chess_engine.GetWhiteToMove() != team_white || !all_lines)
                    {
                        OnBtnSwitch(null, null);
                    }

                    updateScore();
                });

                Debug.WriteLine("Loaded " + temp_ + " moves!");
            });
        }

        private int LoadDB(string db_path)
        {
            int current_depth = -1;
            LineMove current_move = new LineMove("", null, "", current_depth);
            current_line = current_move;

            Debug.WriteLine("Loading ChessApp.data." + db_path);

            var assembly = IntrospectionExtensions.GetTypeInfo(typeof(Trainingboard)).Assembly;
            Stream stream = assembly.GetManifestResourceStream("ChessApp.data." + db_path);
            string text = "";
            using (var reader = new System.IO.StreamReader(stream))
            {
                text = reader.ReadToEnd();
            }
            string[] content = text.Split('\n');

            int current_line_ = 1;
            int last_percent = 0;

            int current_line__ = 1;
            int last_percent_ = 0;

            bool pruning_active = false;

            foreach (string line in content)
            {
                if ((int)(((float)current_line_ / (float)content.Length) * 100) > last_percent)
                {
                    last_percent = (int)(((float)current_line_ / (float)content.Length) * 100);

                    Device.BeginInvokeOnMainThread(() =>
                    {
                        label_task.Text = "Loading DB (" + last_percent + "%)" + (!all_lines ? "- Sorting lines (" + last_percent_ + "%)" : "") + ", please wait ...";
                    });
                }

                string[] parts = line.Replace("\r", "").Split('|');

                if (parts.Length < 4)
                {
                    continue;
                }

                if (parts[0].Length == current_depth)
                {
                    if (pruning_active && pruning)
                    {
                        current_move.move_before.moves_after.Remove(current_move);
                    }
                    else
                    {
                        // End of line
                        line_ends.Add(current_move);
                    }

                    LineMove temp = current_move.move_before;
                    current_move = new LineMove(parts[1], temp, parts[3], current_depth);
                    temp.moves_after.Add(current_move);
                }
                else if (parts[0].Length > current_depth)
                {



                    current_depth += 1;
                    LineMove temp = current_move;
                    current_move = new LineMove(parts[1], temp, parts[3], current_depth);
                    temp.moves_after.Add(current_move);

                    pruning_active = false;
                }
                else
                {
                    if (pruning_active && pruning)
                    {
                        current_move.move_before.moves_after.Remove(current_move);
                    }
                    else
                    {
                        // End of line
                        line_ends.Add(current_move);
                    }

                    while (current_depth > parts[0].Length)
                    {
                        current_depth -= 1;
                        current_move = current_move.move_before;
                    }

                    LineMove temp = current_move.move_before;
                    current_move = new LineMove(parts[1], temp, parts[3], current_depth);
                    temp.moves_after.Add(current_move);

                    pruning_active = true;
                }

                current_line_++;
            }

            if (!all_lines)
            {
                foreach (LineMove lm in line_ends)
                {
                    line_dict.insertLine(lm);

                    if ((int)(((float)current_line__ / (float)line_ends.Count) * 100) > last_percent_)
                    {
                        last_percent_ = (int)(((float)current_line__ / (float)line_ends.Count) * 100);

                        Device.BeginInvokeOnMainThread(() =>
                        {
                            label_task.Text = "Loading DB (" + last_percent + "%)" + (!all_lines ? "- Sorting lines (" + last_percent_ + "%)" : "") + ", please wait ...";
                        });
                    }

                    current_line__++;
                }
            }

            Debug.WriteLine("Found " + current_line.GetLineSum() + " line moves.");
            //current_line.PrintLine();

            return content.Length;
        }

        private void OnBtnSwitch(object sender, EventArgs args)
        {
            if (all_lines)
            {
                SetNewMove();
            }
            else
            {
                SetNewLine(line_dict);
            }
        }

        private async Task<bool> SetNewLine(LineDict lineDict)
        {
            string[] options = new string[lineDict.afters.Count];

            int current_index = 0;
            foreach (LineDict ld in lineDict.afters)
            {
                options[current_index] = (ld.afters.Count > 0 ? ld.name : ld.orig_name);
                current_index++;
            }

            bool done_ = false;
            string action = null;

            while (!done_)
            {
                do_save = false;
                action = await SelectionPage.Choose("Choose line...", current_line.move_before != null ? "One step back" : "", "Cancel", options);
                do_save = true;

                if (action == null)
                {
                    return false;
                }
                else if (action == "Cancel")
                {
                    return true;
                }
                else if (action == "One step back")
                {
                    // Reset two moves
                    current_line = current_line.move_before;
                    pos_to_change.AddRange(chess_engine.ReverseMove());

                    if (current_line.move_before != null)
                    {
                        current_line = current_line.move_before;
                        pos_to_change.AddRange(chess_engine.ReverseMove());
                    }

                    foreach (int pos_ in pos_to_change)
                    {
                        updateImage(pos_, chess_engine.chess_field[pos_].GetImage());
                    }
                    pos_to_change.Clear();

                    done_ = true;
                }
                else
                {
                    int index = Array.FindIndex(options, x => x == action);

                    if (lineDict.afters[index].afters.Count > 0)
                    {
                        done_ = await SetNewLine(lineDict.afters[index]);

                        if (done_)
                        {
                            return true;
                        }
                    }
                    else
                    {
                        // Init line
                        current_line = lineDict.afters[index].lm;
                        current_line_indices = new List<int>();
                        final_line_name = current_line.line_name;

                        while (current_line.move_before != null)
                        {
                            LineMove temp = current_line;
                            current_line = current_line.move_before;
                            current_line_indices.Insert(0, current_line.moves_after.IndexOf(temp));
                        }

                        // Reset board
                        chess_engine.ResetGame();

                        updateBoard();
                        pos_to_change.Clear();

                        done_ = true;
                    }
                }

                action = null;
            }

            // Start if not white
            if (chess_engine.GetWhiteToMove() != team_white && current_line.moves_after.Count() > 0)
            {
                current_line = current_line.moves_after[current_line_indices[current_line.depth + 1]];

                // Move from pgn and show
                pos_to_change.AddRange(chess_engine.MoveFromPGN(current_line.move_name));

                foreach (int pos_ in pos_to_change)
                {
                    updateImage(pos_, chess_engine.chess_field[pos_].GetImage());
                }
                pos_to_change.Clear();
            }

            label_moves.Text = "";
            corrects = 0;
            mistakes = 0;
            solution_shown = false;

            updateScore();

            return true;
        }

        private async Task SetNewMove()
        {
            string[] options = new string[current_line.moves_after.Count];

            int current_index = 0;
            foreach (LineMove lineMove in current_line.moves_after)
            {
                options[current_index] = lineMove.move_name + " (" + lineMove.line_name + ")";
                current_index++;
            }

            do_save = false;
            string action = await SelectionPage.Choose("Choose line...", current_line.move_before != null ? "One step back" : "", "Cancel", options);
            do_save = true;

            if (action == null || action == "Cancel")
            {
                return;
            }
            else if(action == "One step back")
            {
                // Reset and ask again
                current_line = current_line.move_before;

                pos_to_change.AddRange(chess_engine.ReverseMove());
            }
            else
            {
                int index = Array.FindIndex(options, x => x == action);
                current_line = current_line.moves_after[index];

                // Move piece and continue
                pos_to_change.AddRange(chess_engine.MoveFromPGN(current_line.move_name));
            }

            foreach (int pos_ in pos_to_change)
            {
                updateImage(pos_, chess_engine.chess_field[pos_].GetImage());
            }
            pos_to_change.Clear();

            if (chess_engine.GetWhiteToMove() != team_white)
            {
                await SetNewMove();
            }

            label_moves.Text = "";
            corrects = 0;
            mistakes = 0;
            solution_shown = false;

            updateScore();
        }

        private async Task ShowPopup(string message, Color bg_color, float popup_time)
        {
            Color old_col = this.task_frame.BackgroundColor;
            task_frame.BackgroundColor = bg_color;

            label_task.Text = message;

            await Task.WhenAny<bool>
                (
                this.task_frame.FadeTo(0, (uint)(popup_time * 1000), Easing.CubicIn)
                );

            task_frame.BackgroundColor = old_col;
            task_frame.Opacity = 100;
        }

        protected override void OnParentSet()
        {
            base.OnParentSet();

            Debug.WriteLine("Closing page, saving?");

            if (do_save) SaveData();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            if (do_save) Debug.WriteLine("Navigating page, saving?");

            if (do_save) SaveData();
        }

        private void SaveData()
        {

            File.WriteAllText(Path.Combine(FileSystem.AppDataDirectory, "learning", DateTime.Now.ToString("yyyy_MM_dd_") +  "score_" + db_path.Replace(".txt", "") + "_" + (team_white ? "w" : "b") + "_" + (all_lines ? "a" : "s") + ".txt"), ((int)current_score).ToString());

            if (current_score >= high_score)
            {
                File.WriteAllText(Path.Combine(FileSystem.AppDataDirectory, "learning", "highscore_" + db_path.Replace(".txt", "") + "_" + (team_white ? "w" : "b") + "_" + (all_lines ? "a" : "s") + ".txt"), ((int)current_score).ToString());
            }
        }

        private async void OnClick(int position_)
        {
            // Debug.WriteLine(position_.ToString());

            if (clicked_pos_ != -1 && pos_to_change.Contains(position_))
            {
                // Promotion check
                string action = "";
                if ((position_ / 8 == 0 || position_ / 8 == 7) && chess_engine.GetPiece(clicked_pos_) is PiecePawn)
                {
                    do_save = false;
                    action = await SelectionPage.Choose("Promote to ...", "", "Cancel", "Queen", "Rook", "Bishop", "Knight");
                    do_save = true;

                    if (action == null || action == "Cancel")
                    {
                        return;
                    }
                }

                //string move_pgn = chess_engine.GetMovePGN(clicked_pos_, position_, action);
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

                string move_pgn = chess_engine.GetLastMovePGN();

                Debug.WriteLine("Move PGN: " + move_pgn);

                if (all_lines)
                {
                    // Calculate stuff
                    if (!label_moves.Text.Replace(" ", "").Split("|".ToCharArray()).Contains(move_pgn))
                    {
                        LineMove lineMove = current_line.moves_after.Find(x => x.move_name == move_pgn);
                        if (lineMove != null)
                        {
                            current_score += 100 * 1 / (float)Math.Sqrt(current_line.moves_after.Count);
                            corrects += 1;

                            label_moves.Text += (label_moves.Text.Length > 0 ? " | " : "") + lineMove.move_name;

                            await ShowPopup("Good move!\n(" + lineMove.line_name + ")", new Color(0.5, 0.9, 0.5), popup_time);

                            if (corrects == current_line.moves_after.Count())
                            {
                                await ShowPopup("You found all moves!\nContinue by switching the line.", new Color(0.3, 0.9, 0.3), popup_time);
                            }
                        }
                        else
                        {
                            current_score -= 100 * 1 / current_line.moves_after.Count;
                            current_score = current_score < 0 ? 0 : current_score;

                            mistakes += 1;
                            await ShowPopup("Move is not a good option!\n", new Color(0.9, 0.5, 0.5), popup_time);
                        }
                    }
                    else
                    {
                        await ShowPopup("Move already found!\n", new Color(0.7, 0.7, 0.5), popup_time);
                    }

                    updateScore();

                    temp_to_change = chess_engine.ReverseMove();

                    // Change temp images
                    foreach (int pos in temp_to_change)
                    {
                        updateImage(pos, chess_engine.chess_field[pos].GetImage());
                    }
                }
                else
                {
                    // Calculate stuff
                    if (current_line.moves_after.Count() > 0)
                    {
                        if (move_pgn == current_line.moves_after[current_line_indices[current_line.depth + 1]].move_name)
                        {
                            current_line = current_line.moves_after[current_line_indices[current_line.depth + 1]];

                            current_score += 100;

                            await ShowPopup("Move is correct!\n", new Color(0.5, 0.9, 0.5), popup_time);

                            // Proceed with line
                            if (current_line.moves_after.Count() > 0)
                            {
                                current_line = current_line.moves_after[current_line_indices[current_line.depth + 1]];

                                if (this.repetition && current_line.depth > 4)
                                {
                                    bool is_repetition = false;
                                    while (current_line.moves_after.Count() > 0 &&
                                        current_line.move_before.move_before.move_before.move_name == current_line.moves_after[current_line_indices[current_line.depth + 1]].move_name &&
                                        current_line.moves_after[current_line_indices[current_line.depth + 1]].moves_after.Count() > 0 &&
                                        current_line.move_before.move_before.move_name == current_line.moves_after[current_line_indices[current_line.depth + 1]].moves_after[current_line_indices[current_line.depth + 2]].move_name &&
                                        current_line.moves_after[current_line_indices[current_line.depth + 1]].moves_after[current_line_indices[current_line.depth + 2]].moves_after.Count() > 0 &&
                                        current_line.move_before.move_name == current_line.moves_after[current_line_indices[current_line.depth + 1]].moves_after[current_line_indices[current_line.depth + 2]].moves_after[current_line_indices[current_line.depth + 3]].move_name &&
                                        current_line.moves_after[current_line_indices[current_line.depth + 1]].moves_after[current_line_indices[current_line.depth + 2]].moves_after[current_line_indices[current_line.depth + 3]].moves_after.Count() > 0 &&
                                        current_line.move_name == current_line.moves_after[current_line_indices[current_line.depth + 1]].moves_after[current_line_indices[current_line.depth + 2]].moves_after[current_line_indices[current_line.depth + 3]].moves_after[current_line_indices[current_line.depth + 4]].move_name)
                                    {
                                        // Skip 4 moves ahead
                                        current_line = current_line.moves_after[current_line_indices[current_line.depth + 1]].moves_after[current_line_indices[current_line.depth + 2]].moves_after[current_line_indices[current_line.depth + 3]].moves_after[current_line_indices[current_line.depth + 4]];
                                        is_repetition = true;
                                    }

                                    if (is_repetition)
                                    {
                                        await ShowPopup("Skipping ahead!\n", new Color(0.7, 0.7, 0.9), popup_time);
                                    }
                                }

                                // Move from pgn and show
                                pos_to_change.AddRange(chess_engine.MoveFromPGN(current_line.move_name));

                                foreach (int pos_ in pos_to_change)
                                {
                                    updateImage(pos_, chess_engine.chess_field[pos_].GetImage());
                                }
                                pos_to_change.Clear();
                            }
                        }
                        else
                        {
                            current_score -= 100 * 1 / current_line_indices.Count;
                            current_score = current_score < 0 ? 0 : current_score;

                            await ShowPopup("Move is incorrect!\n", new Color(0.9, 0.5, 0.5), popup_time);

                            temp_to_change = chess_engine.ReverseMove();

                            // Change temp images
                            foreach (int pos in temp_to_change)
                            {
                                updateImage(pos, chess_engine.chess_field[pos].GetImage());
                            }
                        }
                    }

                    updateScore();
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

                    if (p != null && p.getTeamWhite() == team_white && p.getTeamWhite() == chess_engine.GetWhiteToMove())
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

        private void updateScore()
        {
            label_score.Text = ((int)current_score).ToString();
            label_highscore.Text = ((int)current_score > high_score ? (int)current_score : high_score).ToString();

            if (all_lines)
            {
                label_trials.Text = corrects.ToString() + " / " + current_line.moves_after.Count() + " [" + mistakes + "]";
                label_task.Text = (solution_shown ?
                                    "Solution given!" :
                                    current_line.moves_after.Count == 0 ?
                                    "End of line!" :
                                    current_line.moves_after.Count - corrects == 0 ?
                                    "All moves found!" :
                                    "Find the remaining " + (current_line.moves_after.Count - corrects) + " correct move" + ((current_line.moves_after.Count - corrects) > 1 ? "s" : "") + "!") + "\n" + current_line.line_name;
            }
            else
            {
                label_trials.Text = "-";
                label_task.Text = final_line_name + "\n" +
                                    (current_line.moves_after.Count == 0 ?
                                    "End of line!" :
                                    "Find the correct move!");
            }
        }

        private async void ShowSolution(object sender, EventArgs args)
        {
            if (all_lines)
            {
                foreach (LineMove lineMove in current_line.moves_after)
                {
                    if (!label_moves.Text.Replace(" ", "").Split("|".ToCharArray()).Contains(lineMove.move_name))
                    {
                        label_moves.Text += (label_moves.Text.Length > 0 ? " | " : "") + lineMove.move_name;
                    }
                }

                solution_shown = true;
            }
            else
            {
                if (current_line.moves_after.Count() > 0)
                {
                    current_line = current_line.moves_after[current_line_indices[current_line.depth + 1]];

                    // Move from pgn and show
                    pos_to_change.AddRange(chess_engine.MoveFromPGN(current_line.move_name));

                    foreach (int pos_ in pos_to_change)
                    {
                        updateImage(pos_, chess_engine.chess_field[pos_].GetImage());
                    }
                    pos_to_change.Clear();

                    await ShowPopup("The correct move was:\n" + current_line.move_name, new Color(0.9, 0.9, 0.5), popup_time);

                    if (current_line.moves_after.Count() > 0)
                    {

                        current_line = current_line.moves_after[current_line_indices[current_line.depth + 1]];

                        // Move from pgn and show
                        pos_to_change.AddRange(chess_engine.MoveFromPGN(current_line.move_name));

                        foreach (int pos_ in pos_to_change)
                        {
                            updateImage(pos_, chess_engine.chess_field[pos_].GetImage());
                        }
                        pos_to_change.Clear();
                    }
                }
            }

            updateScore();
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
            for (int i = 0; i < 64; i++)
            {
                chess_engine.chess_field[i].SetMoved(false);
                updateImage(i, chess_engine.chess_field[i].GetImage());
            }
        }

        private void updateBoard(object sender, EventArgs args)
        {
            updateBoard();
        }

        private bool updateImage(int pos_, Image img_) 
        {
            return updateImage(pos_, img_, 0);
        }

        private bool updateImage(int pos_, Image img_, int trials)
        {
            try
            {
                Grid.SetColumn(img_, !team_white ? 7 - pos_ % 8 : pos_ % 8);
                Grid.SetRow(img_, !team_white ? 7 - pos_ / 8 : pos_ / 8);

                mygrid.Children.RemoveAt(pos_);
                mygrid.Children.Insert(pos_, img_);
                return true;
            }
            catch (Exception)
            {
                if (trials == 0)
                {
                    Debug.WriteLine("Error when trying to update image, resetting board");

                    mygrid.Children.Clear();

                    for (int i = 0; i < 64; i++)
                    {
                        mygrid.Children.Add(chess_engine.chess_field[i].GetImage());
                    }

                    return updateImage(pos_, img_, 1);

                }
                else
                {
                    DisplayAlert("Error", "Error when trying to update images, exiting", "OK");

                    SaveData();

                    System.Environment.Exit(0);
                }

                return false;
            }
        }

    }

    public class LineMove
    {
        public string move_name;
        public LineMove move_before;
        public List<LineMove> moves_after;
        public string line_name;
        public int depth;

        public LineMove(string move_name, LineMove before, string line_name, int depth)
        {
            this.move_name = move_name;
            this.move_before = before;
            this.moves_after = new List<LineMove>();
            this.line_name = line_name;
            this.depth = depth;
        }

        public void PrintLine()
        {
            Debug.WriteLine(depth.ToString() + ": " + move_name + " (" + line_name + ") -> options: " + moves_after.Count);
            foreach (LineMove element in moves_after)
            {
                element.PrintLine();
            }
        }

        public int GetLineSum()
        {
            int sum = moves_after.Count();
            foreach (LineMove element in moves_after)
            {
                sum += element.GetLineSum();
            }

            return sum;
        }

        public string OrderUntil()
        {
            return (this.move_before != null ? move_before.OrderUntil() + " - " : "") + move_name;
        }
    }

    public class LineDict
    {
        private const string ESTR = "!~!";
        public string name;
        public string orig_name;
        public List<LineDict> afters = new List<LineDict>();
        public LineMove lm = null;

        public LineDict(string name_, LineMove lineMove)
        {
            name = name_;
            lm = lineMove;
            orig_name = lineMove != null ? lineMove.line_name : name_;
        }

        public bool insertLine(LineMove lineMove)
        {
            return insertLine(lineMove, lineMove.line_name);
        }

        public bool insertLine(LineMove lineMove, string orig_name_)
        {
            // Debug.WriteLine("Checking: " + name + " ~ " + orig_name_);

            if ("Checking:  ~ C95 Ruy Lopez: Morphy Defense, Breyer Defense, Zaitsev Hybrid" == "Checking: " + name + " ~ " + orig_name_)
            {

            }

            if (afters.Count > 0 || name == "")
            {
                if (orig_name_.Contains(name))
                {
                    bool found_ = false;
                    foreach (LineDict ld in afters)
                    {
                        if (ld.insertLine(lineMove, name != "" ? orig_name_.Replace(name, ESTR) : orig_name_))
                        {
                            found_ = true;
                            break;
                        }
                    }

                    if (!found_)
                    {
                        afters.Add(new LineDict(name != "" ? orig_name_.Replace(name, ESTR) : orig_name_, lineMove));
                    }

                    return true;
                }
                else
                {
                    return false;
                }
                
            }
            else
            {
                // Check for colon
                string[] split_ = name.Split(':');

                if (split_.Length > 1 && !split_[0].Contains(ESTR))
                {
                    string line_name = split_[0].Substring(split_[0].IndexOf(' ') + 1);

                    if (orig_name_.Contains(line_name))
                    {
                        afters.Add(new LineDict(name.Replace(line_name, ESTR), lm));
                        name = line_name.Trim();
                        lm = null;

                        //return afters[afters.Count - 1].insertLine(lineMove, orig_name_.Replace(line_name, ESTR));

                        if (!afters[afters.Count - 1].insertLine(lineMove, orig_name_.Replace(line_name, ESTR)))
                        {
                            afters.Add(new LineDict(orig_name_.Replace(line_name, ESTR), lineMove));
                        }

                        return true;
                    }
                    else
                    {
                        return false;
                    }

                }
                else
                {
                    // Check for commata match
                    split_ = split_[split_.Length - 1].Split(',');

                    if (split_.Length > 0)
                    {
                        bool found_ = false;
                        int ind = 0;
                        foreach (string line_name in split_)
                        {
                            if (orig_name_.Contains(line_name) && !line_name.Contains(ESTR))
                            {
                                afters.Add(new LineDict(name.Replace(line_name, ESTR), lm));
                                name = line_name.Trim();
                                lm = null;

                                if (ind < split_.Length - 1)
                                {
                                    if (!afters[afters.Count - 1].insertLine(lineMove, orig_name_.Replace(line_name, ESTR)))
                                    {
                                        afters.Add(new LineDict(orig_name_.Replace(line_name, ESTR), lineMove));
                                    }
                                }
                                else
                                {
                                    afters.Add(new LineDict(orig_name_.Replace(line_name, ESTR), lineMove));
                                }
                                found_ = true;

                                break;
                            }

                            ind++;
                        }

                        return found_;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }
    }
}