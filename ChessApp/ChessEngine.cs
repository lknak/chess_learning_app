using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using Xamarin.Forms;

namespace ChessApp
{

    class ChessEngine
    {

        public List<Piece> pieces_list { get; } = new List<Piece>();

        private bool white_to_move = true;
        private int en_passant = -1;
        private string[] rochade = new string[] { "K", "Q", "k", "q" };
        private int half_moves = 0;
        private int move_nr = 0;
        private int gamestate = 0;
        private Action<int> onClickMethod;

        private int white_king_position;
        private int black_king_position;

        public Field[] chess_field;
        private List<Move> moves_list = new List<Move>();

        public RochadeComb[] ROCHADE_COMBS { get; } = new RochadeComb[]
        {
            new RochadeComb {team=true, rochade="K", position=-62, in_between_positions=new List<int> {60, 61} },
            new RochadeComb {team=true, rochade="Q", position=-58, in_between_positions=new List<int> {60, 59} },
            new RochadeComb {team=false, rochade="k", position=-6, in_between_positions=new List<int> {4, 5} },
            new RochadeComb {team=false, rochade="q", position=-2, in_between_positions=new List<int> {4, 3} }
        };

        public string[] FIELD_NAMES { get; } = new string[]
        {
            "a8", "b8", "c8", "d8", "e8", "f8", "g8", "h8",
            "a7", "b7", "c7", "d7", "e7", "f7", "g7", "h7",
            "a6", "b6", "c6", "d6", "e6", "f6", "g6", "h6",
            "a5", "b5", "c5", "d5", "e5", "f5", "g5", "h5",
            "a4", "b4", "c4", "d4", "e4", "f4", "g4", "h4",
            "a3", "b3", "c3", "d3", "e3", "f3", "g3", "h3",
            "a2", "b2", "c2", "d2", "e2", "f2", "g2", "h2",
            "a1", "b1", "c1", "d1", "e1", "f1", "g1", "h1"
        };

        public ChessEngine(Action<int> onClickMethod, Field[] fields)
        {
            chess_field = fields;
            this.onClickMethod = onClickMethod;

            Init_board(onClickMethod);
        }

        public ChessEngine(Action<int> onClickMethod, Field[] fields, string init_string)
        {

            chess_field = fields;
            this.onClickMethod = onClickMethod;

            // Init string can be either FEN or PGN

            if (init_string.Split("/".ToCharArray()).Length == 8)
            {
                Debug.WriteLine("Reconstructing position from FEN " + init_string);

                // Decode FEN
                string[] string_parts = init_string.Split(" ".ToCharArray());

                // 1. board position
                string[] rows = string_parts[0].Split("/".ToCharArray());
                int board_pos = 0;
                foreach(string row in rows)
                {
                    foreach(char ch in row.ToCharArray()) {

                        int res;
                        if (int.TryParse(ch.ToString(), out res)) {
                            board_pos += res;
                        }
                        else
                        {
                            switch (ch.ToString())
                            {
                                case "P":
                                    pieces_list.Add(new PiecePawn(true, board_pos, onClickMethod));
                                    break;
                                case "p":
                                    pieces_list.Add(new PiecePawn(false, board_pos, onClickMethod));
                                    break;
                                case "N":
                                    pieces_list.Add(new PieceKnight(true, board_pos, onClickMethod));
                                    break;
                                case "n":
                                    pieces_list.Add(new PieceKnight(false, board_pos, onClickMethod));
                                    break;
                                case "B":
                                    pieces_list.Add(new PieceBishop(true, board_pos, onClickMethod));
                                    break;
                                case "b":
                                    pieces_list.Add(new PieceBishop(false, board_pos, onClickMethod));
                                    break;
                                case "R":
                                    pieces_list.Add(new PieceRook(true, board_pos, onClickMethod));
                                    break;
                                case "r":
                                    pieces_list.Add(new PieceRook(false, board_pos, onClickMethod));
                                    break;
                                case "Q":
                                    pieces_list.Add(new PieceQueen(true, board_pos, onClickMethod));
                                    break;
                                case "q":
                                    pieces_list.Add(new PieceQueen(false, board_pos, onClickMethod));
                                    break;
                                case "K":
                                    pieces_list.Add(new PieceKing(true, board_pos, onClickMethod));
                                    white_king_position = board_pos;
                                    break;
                                case "k":
                                    pieces_list.Add(new PieceKing(false, board_pos, onClickMethod));
                                    black_king_position = board_pos;
                                    break;
                                default:
                                    break;
                            }
                            board_pos++;
                        }
                    }
                }

                updateFields();

                // 2. move team
                white_to_move = (string_parts[1] == "w");

                // 3. rochade
                rochade = string_parts[2].Select(x => x.ToString()).ToArray();

                // 4. en passant
                int.TryParse(string_parts[3], out en_passant);

                // 5. half moves
                int.TryParse(string_parts[4], out half_moves);

                // 6. move nr
                int.TryParse(string_parts[5], out move_nr);
            }
            else
            {
                Debug.WriteLine("Reconstructing position from PGN " + init_string);

                // Decode PGN
                Init_board(onClickMethod);

                string[] string_parts = init_string.Split(" ".ToCharArray());

                int s_nr = 0;
                foreach (string s in string_parts)
                {
                    if (s.Length > 0 && !s.EndsWith("."))
                    {

                        string[] e_arr = s.Split(".".ToCharArray());

                        string move = e_arr[e_arr.Length - 1];

                        MoveFromPGN(move);
                    }

                    s_nr++;
                }
            }
        }

        public void ResetGame()
        {
            pieces_list.Clear();
            white_to_move = true;
            en_passant = -1;
            rochade = new string[] { "K", "Q", "k", "q" };
            half_moves = 0;
            move_nr = 0;
            gamestate = 0;
            moves_list = new List<Move>();

            foreach(Field f in chess_field)
            {
                f.RemovePiece();
            }

            Init_board(this.onClickMethod);
        }

        public List<int> MoveFromPGN(string move)
        {
            List<int> return_list = new List<int>();
            string orig_pgn = move;
            move = move.Replace("+", "").Replace("#", "").Replace("x", "");

            Debug.WriteLine("PGN string " + orig_pgn + " (" + move + ")");

            if (move == "O-O")
            {
                if (white_to_move)
                {
                    return_list.AddRange(MovePiece(60, 62, "", orig_pgn));
                    return_list.Add(60);
                    return_list.Add(62);
                }
                else
                {
                    return_list.AddRange(MovePiece(4, 6, "", orig_pgn));
                    return_list.Add(4);
                    return_list.Add(6);
                }
                Debug.WriteLine("Performing kingside castle");
            }
            else if (move == "O-O-O")
            {
                if (white_to_move)
                {
                    return_list.AddRange(MovePiece(60, 58, "", orig_pgn));
                    return_list.Add(60);
                    return_list.Add(58);
                }
                else
                {
                    return_list.AddRange(MovePiece(4, 2, "", orig_pgn));
                    return_list.Add(4);
                    return_list.Add(2);
                }
                Debug.WriteLine("Performing queenside castle");
            }
            else
            {
                // Get position, care for promotion
                string promotion = "";
                if (move.Contains("="))
                {
                    promotion = move.Substring(move.Length - 2, 2);
                    move = move.Substring(0, move.Length - 2);
                }

                string encoded_pos = move.Substring(move.Length - 2, 2);
                int pos_new_ = 0;
                foreach (string field_name in FIELD_NAMES)
                {
                    if (field_name == encoded_pos) break;
                    pos_new_++;
                }
                return_list.Add(pos_new_);

                // Get piecetype
                string piece_type;
                if (move.Length == 2 || Char.IsLower(move, 0))
                {
                    piece_type = "P";
                    move = "P" + move;
                }
                else
                {
                    piece_type = move.Substring(0, 1);
                }

                // Get extra constraints
                int row_constraint = -1;
                string col_constraint = "";
                if (move.Length > 3)
                {
                    if (!int.TryParse(move.Substring(1, 1), out row_constraint))
                    {
                        col_constraint = move.Substring(1, 1);
                        row_constraint = -1;

                        if (move.Length > 4)
                        {
                            if (!int.TryParse(move.Substring(2, 1), out row_constraint))
                            {
                                row_constraint = -1;
                            }
                        }
                    }
                }

                // Move correct piece
                bool moved_ = false;
                foreach (Piece p in pieces_list)
                {
                    if (white_to_move == p.getTeamWhite() &&
                        piece_type == p.GetTag().ToUpper() &&
                        !(row_constraint >= 0 && int.Parse(FIELD_NAMES[p.getPosition()].Substring(1, 1)) != row_constraint) &&
                        !(col_constraint.Length > 0 && FIELD_NAMES[p.getPosition()].Substring(0, 1) != col_constraint))
                    {
                        Debug.WriteLine("Checking piece W: " + white_to_move + " -- Type:" +
                        piece_type + " -- ROW: " +
                        int.Parse(FIELD_NAMES[p.getPosition()].Substring(1, 1)) + " -- COL: " +
                        FIELD_NAMES[p.getPosition()].Substring(0, 1));

                        if (p.getMoves(this).Contains(pos_new_))
                        {
                            Debug.WriteLine("Moving piece " + p.GetType().ToString() + " from pos " + p.getPosition() + " to pos " + pos_new_);

                            return_list.Insert(0, p.getPosition());

                            return_list.AddRange(MovePiece(p.getPosition(), pos_new_, promotion, orig_pgn));
                            moved_ = true;
                            break;
                        }
                    }
                }
                if (!moved_)
                {
                    Debug.WriteLine("Did not find piece to move!");
                }
            }

            return return_list;
        }

        private void Init_board(Action<int> onClickMethod)
        {
            // Initialize standard board

            // White
            pieces_list.Add(new PiecePawn(true, 48, onClickMethod));
            pieces_list.Add(new PiecePawn(true, 49, onClickMethod));
            pieces_list.Add(new PiecePawn(true, 50, onClickMethod));
            pieces_list.Add(new PiecePawn(true, 51, onClickMethod));
            pieces_list.Add(new PiecePawn(true, 52, onClickMethod));
            pieces_list.Add(new PiecePawn(true, 53, onClickMethod));
            pieces_list.Add(new PiecePawn(true, 54, onClickMethod));
            pieces_list.Add(new PiecePawn(true, 55, onClickMethod));

            pieces_list.Add(new PieceRook(true, 56, onClickMethod));
            pieces_list.Add(new PieceKnight(true, 57, onClickMethod));
            pieces_list.Add(new PieceBishop(true, 58, onClickMethod));
            pieces_list.Add(new PieceQueen(true, 59, onClickMethod));
            pieces_list.Add(new PieceKing(true, 60, onClickMethod));
            pieces_list.Add(new PieceBishop(true, 61, onClickMethod));
            pieces_list.Add(new PieceKnight(true, 62, onClickMethod));
            pieces_list.Add(new PieceRook(true, 63, onClickMethod));

            // Black
            pieces_list.Add(new PiecePawn(false, 8, onClickMethod));
            pieces_list.Add(new PiecePawn(false, 9, onClickMethod));
            pieces_list.Add(new PiecePawn(false, 10, onClickMethod));
            pieces_list.Add(new PiecePawn(false, 11, onClickMethod));
            pieces_list.Add(new PiecePawn(false, 12, onClickMethod));
            pieces_list.Add(new PiecePawn(false, 13, onClickMethod));
            pieces_list.Add(new PiecePawn(false, 14, onClickMethod));
            pieces_list.Add(new PiecePawn(false, 15, onClickMethod));

            pieces_list.Add(new PieceRook(false, 0, onClickMethod));
            pieces_list.Add(new PieceKnight(false, 1, onClickMethod));
            pieces_list.Add(new PieceBishop(false, 2, onClickMethod));
            pieces_list.Add(new PieceQueen(false, 3, onClickMethod));
            pieces_list.Add(new PieceKing(false, 4, onClickMethod));
            pieces_list.Add(new PieceBishop(false, 5, onClickMethod));
            pieces_list.Add(new PieceKnight(false, 6, onClickMethod));
            pieces_list.Add(new PieceRook(false, 7, onClickMethod));

            white_king_position = 60;
            black_king_position = 4;

            updateFields();
        }

        public void updateFields()
        {
            foreach (Piece p in pieces_list)
            {
                chess_field[p.getPosition()].AssignPiece(p);
            }
        }

        public Piece GetPiece(int position_)
        {
            foreach (Piece p in pieces_list)
            {
                if (p.getPosition() == position_)
                {
                    return p;
                }
            }

            return null;
        }
        public List<int> MovePiece(int pos_old_, int pos_new_, string promotion)
        {
            return MovePiece(pos_old_, pos_new_, promotion, "");
        }

        public List<int> MovePiece(int pos_old_, int pos_new_, string promotion, string pgn_str)
        {

            List<int> return_list = new List<int>();

            Piece piece = GetPiece(pos_old_);
            Piece piece_new = GetPiece(pos_new_);

            if (piece_new != null)
            {
                pieces_list.Remove(piece_new);
            }

            piece.Move(pos_new_);

            Move move = new Move { 
                moved_piece = piece, 
                captured_piece = piece_new, 
                rochade_piece = null, 
                promotion_piece = null, 
                description = pgn_str != "" ? pgn_str : !(piece is PiecePawn) ? piece.GetTag().ToUpper() : "", 
                en_passant_before = en_passant, 
                gamestate_before = gamestate, 
                rochade_before= (string[])rochade.Clone(),
                before=pos_old_,
                after=pos_new_
            };

            if (pgn_str == "")
            {
                // Check if there is a same piece that can get to this position
                bool same_row = false;
                bool same_col = false;
                foreach (Piece p in pieces_list)
                {
                    if (piece.getTeamWhite() == p.getTeamWhite() &&
                        piece.GetType() == p.GetType() &&
                        piece != p)
                    {
                        if (p.getMoves(this).Contains(pos_new_))
                        {
                            Debug.WriteLine("Found same move option for other piece of type " + p.GetType().ToString() + " at pos " + p.getPosition());
                            same_col = (same_col && (p.getPosition() % 8 == pos_old_ % 8));
                            same_row = (same_row && (p.getPosition() / 8 == pos_old_ / 8)) || !same_col;
                        }
                    }
                }
                move.description += same_row ? FIELD_NAMES[pos_old_].Substring(0, 1) : "";
                move.description += same_col ? FIELD_NAMES[pos_old_].Substring(1, 1) : "";
                move.description += piece_new != null ? (piece is PiecePawn ? FIELD_NAMES[pos_old_].Substring(0, 1) + "x" : "x") : "";
            }

            // Update king position and castling rights
            if (piece is PieceKing)
            {
                if (piece.getTeamWhite())
                {
                    white_king_position = pos_new_;
                    rochade[0] = "";
                    rochade[1] = "";

                    if (pos_old_ == 60)
                    {
                        if (pos_new_ == 62)
                        {
                            move.rochade_piece = GetPiece(63);
                            GetPiece(63).Move(61);
                            chess_field[61].AssignPiece(chess_field[63].RemovePiece());
                            return_list.Add(63);
                            return_list.Add(61);
                            move.description = "O-O";
                        }
                        else if (pos_new_ == 58)
                        {
                            move.rochade_piece = GetPiece(56);
                            GetPiece(56).Move(59);
                            chess_field[59].AssignPiece(chess_field[56].RemovePiece());
                            return_list.Add(56);
                            return_list.Add(59);
                            move.description = "O-O-O";
                        }
                    }
                }
                else
                {
                    black_king_position = piece.getPosition();
                    rochade[2] = "";
                    rochade[3] = "";

                    if (pos_old_ == 4)
                    {
                        if (pos_new_ == 2)
                        {
                            move.rochade_piece = GetPiece(0);
                            GetPiece(0).Move(3);
                            chess_field[3].AssignPiece(chess_field[0].RemovePiece());
                            return_list.Add(0);
                            return_list.Add(3);
                            move.description = pgn_str != "" ? move.description : "O-O-O";
                        }
                        else if (pos_new_ == 6)
                        {
                            move.rochade_piece = GetPiece(7);
                            GetPiece(7).Move(5);
                            chess_field[5].AssignPiece(chess_field[7].RemovePiece());
                            return_list.Add(7);
                            return_list.Add(5);
                            move.description = pgn_str != "" ? move.description : "O-O";
                        }
                    }
                }
            }

            // Update castling rights from rooks
            if (piece is PieceRook)
            {
                if (piece.getTeamWhite())
                {
                    if (pos_old_ == 63)
                    {
                        rochade[0] = "";
                    }
                    else if (pos_old_ == 56)
                    {
                        rochade[1] = "";
                    }
                }
                else
                {
                    if (pos_old_ == 0)
                    {
                        rochade[2] = "";
                    }
                    else if (pos_old_ == 7)
                    {
                        rochade[3] = "";
                    }
                }
            }

            // Check en passant capture
            if (piece is PiecePawn && pos_new_ == en_passant)
            {
                if (piece.getTeamWhite())
                {
                    Piece captured_pawn = GetPiece(pos_new_ + 8);
                    pieces_list.Remove(captured_pawn);
                    chess_field[(pos_new_ + 8)].RemovePiece();
                    return_list.Add(pos_new_ + 8);

                    move.description = pgn_str != "" ? move.description : move.description + FIELD_NAMES[pos_old_].Substring(0, 1) + "x";
                    move.captured_piece = captured_pawn;
                }
                else
                {
                    Piece captured_pawn = GetPiece(pos_new_ - 8);
                    pieces_list.Remove(captured_pawn);
                    chess_field[(pos_new_ - 8)].RemovePiece();
                    return_list.Add(pos_new_ - 8);

                    move.description = pgn_str != "" ? move.description : move.description + FIELD_NAMES[pos_old_].Substring(0, 1) + "x";
                    move.captured_piece = captured_pawn;
                }
            }

            // Update move
            move.description = (pgn_str != "" || move.description == "O-O" || move.description == "O-O-O") ? move.description : move.description + FIELD_NAMES[pos_new_];

            // New en passant
            en_passant = -1;
            if (piece is PiecePawn)
            {
                if (piece.getTeamWhite() && pos_new_ - pos_old_ == -16)
                {
                    en_passant = (pos_new_ + pos_old_) / 2;
                }
                else if (!piece.getTeamWhite() && pos_new_ - pos_old_ == 16)
                {
                    en_passant = (pos_new_ + pos_old_) / 2;
                }

                // Check promotion
                if (pos_new_ / 8 == 0 || pos_new_ / 8 == 7)
                {
                    switch (promotion.Substring(0, 1))
                    {
                        case "Q":
                            move.promotion_piece = new PieceQueen(piece.getTeamWhite(), pos_new_, piece.GetOnClickMethod());
                            move.description += "=Q";
                            break;
                        case "R":
                            move.promotion_piece = new PieceRook(piece.getTeamWhite(), pos_new_, piece.GetOnClickMethod());
                            move.description += "=R";
                            break;
                        case "B":
                            move.promotion_piece = new PieceBishop(piece.getTeamWhite(), pos_new_, piece.GetOnClickMethod());
                            move.description += "=B";
                            break;
                        case "K":
                            move.promotion_piece = new PieceKnight(piece.getTeamWhite(), pos_new_, piece.GetOnClickMethod());
                            move.description += "=N";
                            break;
                        case "N":
                            move.promotion_piece = new PieceKnight(piece.getTeamWhite(), pos_new_, piece.GetOnClickMethod());
                            move.description += "=N";
                            break;
                        default:
                            break;
                    }
                    pieces_list.Remove(piece);
                    pieces_list.Add(move.promotion_piece);
                }
            }

            // Update identifiers
            move_nr += white_to_move ? 1 : 0;
            white_to_move = !white_to_move;
            half_moves = (piece is PiecePawn || piece_new != null) ? 0 : half_moves + 1;

            // Update update moves
            foreach (Piece p in pieces_list)
            {
                p.setUpdateMove(true);
            }

            // Update board but include promotion piece
            if (move.promotion_piece != null)
            {
                chess_field[pos_old_].RemovePiece();
                chess_field[pos_new_].AssignPiece(move.promotion_piece);
            }
            else
            {
                chess_field[pos_new_].AssignPiece(chess_field[pos_old_].RemovePiece());
            }

            // Update moved status
            int pos = 0;
            foreach (Field f in chess_field)
            {
                if (f.GetMoved())
                {
                    f.SetMoved(false);
                    return_list.Add(pos);
                }
                pos++;
            }
            chess_field[pos_old_].SetMoved(true);
            chess_field[pos_new_].SetMoved(true);


            // Check and mate stuff
            Debug.WriteLine("Evaluating gamestate");
            // Check for check
            gamestate = 0;
            foreach (Piece p in pieces_list)
            {
                if (p.getTeamWhite() != white_to_move &&
                    !(p is PieceKing) &&
                    p.getMoves(this, false).Contains(GetActiveKingPosition()))
                {
                    gamestate = 1;
                    break;
                }
            }

            // Check for mate
            if (gamestate == 1)
            {
                gamestate = 2;
                foreach (Piece p in pieces_list)
                {
                    // Must check for check because pinned piece cant capture
                    if (p.getTeamWhite() == white_to_move &&
                        p.getMoves(this).Count() > 0)
                    {
                        gamestate = 1;
                        break;
                    }
                }
            }

            move.description = pgn_str != "" ? move.description : (move.description + (gamestate == 1 ? "+" : (gamestate == 2 ? "#" : "")));

            moves_list.Add(move);

            return return_list;
        }

        public string GetMovePGN(int pos_old_, int pos_new_, string promotion)
        {
            Piece piece = GetPiece(pos_old_);
            Piece piece_new = GetPiece(pos_new_);

            string description = !(piece is PiecePawn) ? piece.GetTag().ToUpper() : "";

            // Check if there is a same piece that can get to this position
            bool same_row = false;
            bool same_col = false;
            foreach (Piece p in pieces_list)
            {
                if (piece.getTeamWhite() == p.getTeamWhite() &&
                    piece.GetType() == p.GetType() &&
                    piece != p)
                {
                    Debug.WriteLine("Checking move options for other piece of same type " + p.GetType().ToString() + " at pos " + p.getPosition());
                    if (p.getMoves(this).Contains(pos_new_))
                    {
                        same_row = (same_row && (p.getPosition() / 8 == pos_old_ / 8));
                        same_col = (same_col && (p.getPosition() % 8 == pos_old_ % 8));
                    }
                }
            }
            description += same_row ? FIELD_NAMES[pos_old_].Substring(0, 1) : "";
            description += same_col ? FIELD_NAMES[pos_old_].Substring(1, 1) : "";
            description += piece_new != null ? (piece is PiecePawn ? FIELD_NAMES[pos_old_].Substring(0, 1) + "x" : "x") : "";

            // Update king position and castling rights
            if (piece is PieceKing)
            {
                if (piece.getTeamWhite())
                {
                    if (pos_old_ == 60)
                    {
                        if (pos_new_ == 62)
                        {
                            description = "O-O";
                        }
                        else if (pos_new_ == 58)
                        {
                            description = "O-O-O";
                        }
                    }
                }
                else
                {
                    if (pos_old_ == 4)
                    {
                        if (pos_new_ == 2)
                        {
                            description = "O-O-O";
                        }
                        else if (pos_new_ == 6)
                        {
                            description = "O-O";
                        }
                    }
                }
            }

            // Check en passant capture
            if (piece is PiecePawn && pos_new_ == en_passant)
            {
                description += FIELD_NAMES[pos_old_].Substring(0, 1) + "x";
            }

            // Update move
            description = (description == "O-O" || description == "O-O-O") ? description : description + FIELD_NAMES[pos_new_];

            if (piece is PiecePawn)
            {
                // Check promotion
                if (pos_new_ / 8 == 0 || pos_new_ / 8 == 7)
                {
                    switch (promotion.Substring(0, 1))
                    {
                        case "Q":
                            description += "=Q";
                            break;
                        case "R":
                            description += "=R";
                            break;
                        case "B":
                            description += "=B";
                            break;
                        case "K":
                            description += "=N";
                            break;
                        case "N":
                            description += "=N";
                            break;
                        default:
                            break;
                    }
                }
            }

            return description;
        }

        public List<int> ReverseMove()
        {
            List<int> change_positions = new List<int>();

            if (moves_list.Count() > 0)
            {
                Move last_move = moves_list[moves_list.Count() - 1];

                int pos_after = last_move.moved_piece.getPosition();
                int pos_before = last_move.moved_piece.ReverseLastMove();

                // Reverse promotion
                if (last_move.promotion_piece != null)
                {
                    chess_field[pos_after].RemovePiece();
                    pieces_list.Remove(last_move.promotion_piece);

                    chess_field[pos_before].AssignPiece(last_move.moved_piece);
                    pieces_list.Add(last_move.moved_piece);
                }
                else
                {
                    chess_field[pos_before].AssignPiece(chess_field[pos_after].RemovePiece());
                }

                // Reverse capture or rochade
                if (last_move.captured_piece != null)
                {
                    chess_field[last_move.captured_piece.getPosition()].AssignPiece(last_move.captured_piece);
                    pieces_list.Add(last_move.captured_piece);

                    if (last_move.captured_piece.getPosition() != pos_after)
                    {
                        change_positions.Add(last_move.captured_piece.getPosition());
                    }
                }
                else if (last_move.rochade_piece != null)
                {
                    int pos_after_ = last_move.rochade_piece.getPosition();
                    int pos_before_ = last_move.rochade_piece.ReverseLastMove();

                    chess_field[pos_before_].AssignPiece(chess_field[pos_after_].RemovePiece());

                    change_positions.Add(pos_after_);
                    change_positions.Add(pos_before_);
                }

                change_positions.Add(pos_after);
                change_positions.Add(pos_before);

                // Reset king position
                if (last_move.moved_piece is PieceKing)
                {
                    if (last_move.moved_piece.getTeamWhite())
                    {
                        white_king_position = pos_before;
                    }
                    else
                    {
                        black_king_position = pos_before;
                    }
                }

                half_moves = half_moves > 0 ? half_moves - 1 : 0;
                en_passant = last_move.en_passant_before;
                gamestate = last_move.gamestate_before;
                rochade = last_move.rochade_before;
                white_to_move = !white_to_move;
                move_nr -= white_to_move ? 1 : 0;

                moves_list.RemoveAt(moves_list.Count() - 1);

                // Update update moves
                foreach (Piece p in pieces_list)
                {
                    p.setUpdateMove(true);
                }

                // Update moved status
                int pos = 0;
                foreach (Field f in chess_field)
                {
                    if (f.GetMoved())
                    {
                        f.SetMoved(false);
                        change_positions.Add(pos);
                    }
                    pos++;
                }

                if (moves_list.Count() > 0)
                {
                    chess_field[moves_list[moves_list.Count() - 1].before].SetMoved(true);
                    chess_field[moves_list[moves_list.Count() - 1].after].SetMoved(true);
                    change_positions.Add(moves_list[moves_list.Count() - 1].before);
                    change_positions.Add(moves_list[moves_list.Count() - 1].after);
                }
            }

            return change_positions;
        }

        public int GetGamestate()
        {
            return gamestate;
        }

        public bool GetWhiteToMove()
        {
            return white_to_move;
        }

        public int GetActiveKingPosition()
        {
            return white_to_move ? white_king_position : black_king_position;
        }

        public string[] GetRochade()
        {
            return rochade;
        }
        public int GetEnPassant()
        {
            return en_passant;
        }

        public string GetFEN()
        {
            string fen_string = "";

            int cur_empty_field = 0;

            for (int i = 0; i < 64; i++)
            {
                if (i % 8 == 0 && i > 0)
                {
                    fen_string += cur_empty_field > 0 ? cur_empty_field.ToString() : "" + "/";
                    cur_empty_field = 0;
                }

                if (chess_field[i].ContainsPiece())
                {
                    fen_string += cur_empty_field > 0 ? cur_empty_field.ToString() : "" + chess_field[i].GetPiece().GetTag();
                    cur_empty_field = 0;
                }
                else
                {
                    cur_empty_field += 1;
                }
            }

            fen_string += " " + (white_to_move ? "w" : "b");
            fen_string += " " + string.Join("", GetRochade());
            fen_string += " " + (en_passant >= 0 ? FIELD_NAMES[en_passant] : "-");
            fen_string += " " + half_moves.ToString();
            fen_string += " " + (move_nr + (white_to_move ? 1 : 0)).ToString();

            return fen_string;
        }

        public string GetPGN()
        {
            string pgn_string = "";

            int move_nr_ = move_nr * 2 + (white_to_move ? 1 : 0) > moves_list.Count() ? move_nr * 2 + (white_to_move ? 1 : 0) - moves_list.Count() : 1;
            foreach (Move move in moves_list)
            {
                if (move_nr_ % 2 == 1)
                {
                    pgn_string += (move_nr_ > 1 ? " " : "") + ((move_nr_ + 1) / 2).ToString() + ".";
                }

                pgn_string += " " + move.description;

                move_nr_++;
            }

            return pgn_string;
        }

        public string GetLastMovePGN()
        {
            string pgn_string = "";

            if (moves_list.Count > 0)
            {
                pgn_string = moves_list[moves_list.Count() - 1].description;
            }

            return pgn_string;
        }
    }


    class Piece
    {
        protected bool team_white;
        protected int position;
        protected List<int> position_list = new List<int>();
        protected Image image_white;
        protected Image image_black;
        protected Image image_target_black;
        protected Image image_target_white;
        protected Image image_selected_black;
        protected Image image_selected_white;
        protected Image image_moved_black;
        protected Image image_moved_white;
        protected Action<int> _onClickMethod;
        protected TapGestureRecognizer tapGestureRecognizer;

        protected List<int> possible_moves = new List<int>();
        protected bool update_moves = true;

        public Piece()
        {
            initialize(true, 0, new System.Action<int>((str_) => { Debug.WriteLine(str_); }));
        }

        public Piece(bool team_white_, int position_, Action<int> onClickMethod)
        {
            initialize(team_white_, position_, onClickMethod);
        }

        public void initialize(bool team_white_, int position_, Action<int> onClickMethod)
        {
            team_white = team_white_;
            position = position_;
            _onClickMethod = onClickMethod;

            position_list.Add(position_);

            tapGestureRecognizer = new TapGestureRecognizer
            {
                Command = new Command<int>((pos_) =>
                {
                    onClickMethod(pos_);
                }),
                CommandParameter = position,
                NumberOfTapsRequired = 1
            };

            image_black = new Image
            {
                Source = ImageSource.FromResource("ChessApp.images.brown_" + (team_white_ ? "w" : "b") + GetTag().ToLower() + ".png", typeof(MainPage).GetTypeInfo().Assembly)
            };

            image_black.GestureRecognizers.Add(tapGestureRecognizer);


            image_white = new Image
            {
                Source = ImageSource.FromResource("ChessApp.images.white_" + (team_white_ ? "w" : "b") + GetTag().ToLower() + ".png", typeof(MainPage).GetTypeInfo().Assembly)
            };

            image_white.GestureRecognizers.Add(tapGestureRecognizer);


            image_target_black = new Image
            {
                Source = ImageSource.FromResource("ChessApp.images.brown_" + (team_white_ ? "w" : "b") + GetTag().ToLower() + "_target.png", typeof(MainPage).GetTypeInfo().Assembly)
            };

            image_target_black.GestureRecognizers.Add(tapGestureRecognizer);


            image_target_white = new Image
            {
                Source = ImageSource.FromResource("ChessApp.images.white_" + (team_white_ ? "w" : "b") + GetTag().ToLower() + "_target.png", typeof(MainPage).GetTypeInfo().Assembly)
            };

            image_target_white.GestureRecognizers.Add(tapGestureRecognizer);



            image_selected_black = new Image
            {
                Source = ImageSource.FromResource("ChessApp.images.brown_" + (team_white_ ? "w" : "b") + GetTag().ToLower() + "_selected.png", typeof(MainPage).GetTypeInfo().Assembly)
            };

            image_selected_black.GestureRecognizers.Add(tapGestureRecognizer);


            image_selected_white = new Image
            {
                Source = ImageSource.FromResource("ChessApp.images.white_" + (team_white_ ? "w" : "b") + GetTag().ToLower() + "_selected.png", typeof(MainPage).GetTypeInfo().Assembly)
            };

            image_selected_white.GestureRecognizers.Add(tapGestureRecognizer);



            image_moved_black = new Image
            {
                Source = ImageSource.FromResource("ChessApp.images.brown_" + (team_white_ ? "w" : "b") + GetTag().ToLower() + "_moved.png", typeof(MainPage).GetTypeInfo().Assembly)
            };


            image_moved_white = new Image
            {
                Source = ImageSource.FromResource("ChessApp.images.white_" + (team_white_ ? "w" : "b") + GetTag().ToLower() + "_moved.png", typeof(MainPage).GetTypeInfo().Assembly)
            };
        }

        public Action<int> GetOnClickMethod()
        {
            return _onClickMethod;
        }

        public bool getTeamWhite()
        {
            return team_white;
        }
        public int getPosition()
        {
            return position;
        }

        public Image getImage()
        {
            return position % 2 == (position / 8) % 2 ? image_white : image_black;
        }
        public Image getImageTarget()
        {
            return position % 2 == (position / 8) % 2 ? image_target_white : image_target_black;
        }
        public Image getImageSelected()
        {
            return position % 2 == (position / 8) % 2 ? image_selected_white : image_selected_black;
        }
        public Image getImageMoved()
        {
            return position % 2 == (position / 8) % 2 ? image_moved_white : image_moved_black;
        }

        public void setUpdateMove(bool update_)
        {
            update_moves = update_;
        }
        public bool getUpdateMove()
        {
            return update_moves;
        }

        public List<int> getMoves(ChessEngine chess_engine)
        {
            return getMoves(chess_engine, true);
        }

        public List<int> getMoves(ChessEngine chess_engine, bool check_check)
        {


            List<int> return_list = new List<int>();

            if (update_moves)
            {
                possible_moves.Clear();
                update_moves = !check_check;

                // Calculate all moves for piece
                Debug.WriteLine("Calculating moves for " + this.ToString() + " at pos " + position);
                possible_moves = Calculate_moves(chess_engine.chess_field);

                // En passant move
                if (this is PiecePawn)
                {
                    int pos_diff = chess_engine.GetEnPassant() - position;
                    if ((team_white && (pos_diff == -7 || pos_diff == -9)) || (!team_white && (pos_diff == 7 || pos_diff == 9)))
                    {
                        possible_moves.Add(chess_engine.GetEnPassant());
                    }
                }

                // Rochade
                if (this is PieceKing)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        RochadeComb r = chess_engine.ROCHADE_COMBS[i];
                        if (team_white == r.team)
                        {
                            if (possible_moves.Contains(r.position))
                            {
                                if (chess_engine.GetRochade().Contains(r.rochade))
                                {
                                    // Check in between field checks against all other pieces
                                    foreach (Piece p in chess_engine.pieces_list)
                                    {
                                        if (p.getTeamWhite() != team_white)
                                        {
                                            List<int> temp_moves = p.getMoves(chess_engine, false);
                                            if (temp_moves.Contains(r.position) ||
                                                temp_moves.Contains(r.in_between_positions[0]) ||
                                                (r.in_between_positions.Count() > 1 && temp_moves.Contains(r.in_between_positions[1])))
                                            {
                                                Debug.WriteLine("Removing rochade move: " + r.position + " for " + this.GetType() + " because of Piece " + p.GetType() + " at pos " + p.getPosition());
                                                possible_moves.Remove(r.position);
                                                break;
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // Remove rochade move because no longer allowed
                                    possible_moves.Remove(r.position);
                                }

                            }
                        }
                    }

                    // After rochade check done make all moves positive
                    for (int i = 0; i < possible_moves.Count(); i++)
                    {
                        possible_moves[i] = possible_moves[i] < 0 ? -possible_moves[i] : possible_moves[i];
                    }
                }

                // Subtract illegal check moves
                if (check_check)
                {
                    int i = 0;
                    while (i < possible_moves.Count())
                    {
                        // Temporarily move piece
                        int old_pos = this.position;
                        int new_pos = possible_moves[i];

                        Piece old_piece = chess_engine.chess_field[new_pos].AssignPiece(chess_engine.chess_field[this.position].RemovePiece());

                        // Check against all other pieces
                        bool removed_ = false;
                        foreach (Piece p in chess_engine.pieces_list)
                        {
                            if (p.getTeamWhite() != team_white && p != old_piece && (!(p is PieceKing) || this is PieceKing && p is PieceKing) && (p.getUpdateMove() || !p.getUpdateMove() && p.getMoves(chess_engine).Contains(old_pos)))
                            {
                                List<int> temp_moves = p.Calculate_moves(chess_engine.chess_field);
                                if (!(this is PieceKing) && temp_moves.Contains(chess_engine.GetActiveKingPosition()) || this is PieceKing && temp_moves.Contains(new_pos))
                                {
                                    Debug.WriteLine("Removing possible move: " + new_pos + " for " + this.GetType() + " because of Piece " + p.GetType() + " at pos " + p.getPosition());
                                    possible_moves.RemoveAt(i);
                                    removed_ = true;
                                    break;
                                }
                            }
                        }
                        // Change back piece position
                        chess_engine.chess_field[this.position].AssignPiece(chess_engine.chess_field[new_pos].RemovePiece());

                        if (old_piece != null)
                        {
                            chess_engine.chess_field[new_pos].AssignPiece(old_piece);
                        }

                        // Increment
                        i += (removed_ ? 0 : 1);
                    }
                }
            }

            return_list = new List<int>(possible_moves);


            return return_list;
        }

        protected virtual List<int> Calculate_moves(Field[] chess_field)
        {
            return possible_moves;
        }

        public virtual void Move(int position_new_)
        {
            Debug.WriteLine("Move to pos " + position_new_);

            position_list.Add(position);
            position = position_new_;

            tapGestureRecognizer.CommandParameter = position;
        }

        public virtual int ReverseLastMove()
        {
            int position_new_ = position_list[position_list.Count() - 1];
            position_list.RemoveAt(position_list.Count() - 1);

            Debug.WriteLine("Reversing move to pos " + position_new_);

            position = position_new_;

            tapGestureRecognizer.CommandParameter = position;

            return position_new_;
        }

        public virtual string GetTag()
        {
            return team_white ? "" : "";
        }
    }

    class PiecePawn : Piece
    {
        public PiecePawn(bool team_white_, int position_, Action<int> onClickMethod) : base(team_white_, position_, onClickMethod)
        {

        }

        protected override List<int> Calculate_moves(Field[] chess_field)
        {
            List<int> return_list = new List<int>();

            // Single pawn forward
            int new_pos = position + (team_white ? -8 : 8);
            if (0 <= new_pos && new_pos < 64 &&
                !chess_field[new_pos].ContainsPiece())
            {
                return_list.Add(new_pos);

                // Also check for double forward
                if (team_white && position >= 48 && position <= 55 || !team_white && position >= 8 && position <= 15)
                {
                    new_pos = position + (team_white ? -16 : 16);
                    if (!chess_field[new_pos].ContainsPiece())
                    {
                        return_list.Add(new_pos);
                    }
                }
            }

            // Check capture
            for (int i = -1; i < 2; i++)
            {
                new_pos = position + (team_white ? -8 + i : 8 + i);
                if (0 <= new_pos && new_pos < 64 &&
                    0 <= position % 8 + i && position % 8 + i < 8 &&
                    chess_field[new_pos].ContainsPiece() &&
                    chess_field[new_pos].GetPiece().getTeamWhite() != team_white)
                {
                    return_list.Add(new_pos);
                }
                i++;
            }

            return return_list;
        }

        public override string GetTag()
        {
            return team_white ? "P" : "p";
        }
    }
    class PieceKnight : Piece
    {
        public PieceKnight(bool team_white_, int position_, Action<int> onClickMethod) : base(team_white_, position_, onClickMethod)
        {
        }

        protected override List<int> Calculate_moves(Field[] chess_field)
        {
            List<int> return_list = new List<int>();

            // L moves
            for (int i = 0; i < 4; i++)
            {
                int v = (i % 2) * (i < 2 ? 1 : -1);
                int w = ((i + 1) % 2) * (i < 2 ? 1 : -1);
                int factor = (v != 0 ? 1 : 8);

                for (int k = 0; k < 2; k++)
                {
                    int new_pos = position + 8 * 2 * v + 2 * w + (2 * k - 1) * factor;

                    if (0 <= new_pos && new_pos < 64 &&
                        (position % 8 + 2 * w >= 0 && position % 8 + 2 * w < 8 && (factor != 1 || position % 8 + (2 * k - 1) * factor >= 0 && position % 8 + (2 * k - 1) * factor < 8)) &&
                        (!chess_field[new_pos].ContainsPiece() ||
                        chess_field[new_pos].ContainsPiece() &&
                        chess_field[new_pos].GetPiece().getTeamWhite() != team_white))
                    {
                        return_list.Add(new_pos);
                    }
                }
            }

            return return_list;
        }

        public override string GetTag()
        {
            return team_white ? "N" : "n";
        }
    }
    class PieceBishop : Piece
    {
        public PieceBishop(bool team_white_, int position_, Action<int> onClickMethod) : base(team_white_, position_, onClickMethod)
        {
        }

        protected override List<int> Calculate_moves(Field[] chess_field)
        {
            List<int> return_list = new List<int>();

            // Diagonal moves
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    for (int k = 1; k < 8; k++)
                    {
                        int new_pos = position + 8 * (2 * i - 1) * k + (2 * j - 1) * k;

                        if (0 <= new_pos && new_pos < 64 &&
                            position % 8 + (2 * j - 1) * k >= 0 && position % 8 + (2 * j - 1) * k < 8)
                        {
                            if (!chess_field[new_pos].ContainsPiece())
                            {
                                return_list.Add(new_pos);
                            }
                            else if (chess_field[new_pos].ContainsPiece())
                            {
                                if (chess_field[new_pos].GetPiece().getTeamWhite() != team_white)
                                {
                                    return_list.Add(new_pos);
                                }
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            return return_list;
        }

        public override string GetTag()
        {
            return team_white ? "B" : "b";
        }
    }
    class PieceRook : Piece
    {
        public PieceRook(bool team_white_, int position_, Action<int> onClickMethod) : base(team_white_, position_, onClickMethod)
        {
        }

        protected override List<int> Calculate_moves(Field[] chess_field)
        {
            List<int> return_list = new List<int>();

            // Straight moves
            for (int i = 0; i < 4; i++)
            {
                int v = (i % 2) * (i < 2 ? 1 : -1);
                int w = ((i + 1) % 2) * (i < 2 ? 1 : -1);

                for (int k = 1; k < 8; k++)
                {
                    int new_pos = position + 8 * v * k + w * k;

                    if (0 <= new_pos && new_pos < 64 &&
                        position % 8 + w * k >= 0 && position % 8 + w * k < 8)
                    {
                        if (!chess_field[new_pos].ContainsPiece())
                        {
                            return_list.Add(new_pos);
                        }
                        else if (chess_field[new_pos].ContainsPiece())
                        {
                            if (chess_field[new_pos].GetPiece().getTeamWhite() != team_white)
                            {
                                return_list.Add(new_pos);
                            }
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return return_list;
        }

        public override string GetTag()
        {
            return team_white ? "R" : "r";
        }
    }
    class PieceQueen : Piece
    {
        public PieceQueen(bool team_white_, int position_, Action<int> onClickMethod) : base(team_white_, position_, onClickMethod)
        {
        }

        protected override List<int> Calculate_moves(Field[] chess_field)
        {
            List<int> return_list = new List<int>();

            // Diagonal moves
            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 2; j++)
                {
                    for (int k = 1; k < 8; k++)
                    {
                        int new_pos = position + 8 * (2 * i - 1) * k + (2 * j - 1) * k;

                        if (0 <= new_pos && new_pos < 64 &&
                            position % 8 + (2 * j - 1) * k >= 0 && position % 8 + (2 * j - 1) * k < 8)
                        {
                            if (!chess_field[new_pos].ContainsPiece())
                            {
                                return_list.Add(new_pos);
                            }
                            else if (chess_field[new_pos].ContainsPiece())
                            {
                                if (chess_field[new_pos].GetPiece().getTeamWhite() != team_white)
                                {
                                    return_list.Add(new_pos);
                                }
                                break;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }

            // Straight moves
            for (int i = 0; i < 4; i++)
            {
                int v = (i % 2) * (i < 2 ? 1 : -1);
                int w = ((i + 1) % 2) * (i < 2 ? 1 : -1);

                for (int k = 1; k < 8; k++)
                {
                    int new_pos = position + 8 * v * k + w * k;

                    if (0 <= new_pos && new_pos < 64 &&
                        position % 8 + w * k >= 0 && position % 8 + w * k < 8)
                    {
                        if (!chess_field[new_pos].ContainsPiece())
                        {
                            return_list.Add(new_pos);
                        }
                        else if (chess_field[new_pos].ContainsPiece())
                        {
                            if (chess_field[new_pos].GetPiece().getTeamWhite() != team_white)
                            {
                                return_list.Add(new_pos);
                            }
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }

            return return_list;
        }

        public override string GetTag()
        {
            return team_white ? "Q" : "q";
        }
    }
    class PieceKing : Piece
    {
        public PieceKing(bool team_white_, int position_, Action<int> onClickMethod) : base(team_white_, position_, onClickMethod)
        {
        }

        protected override List<int> Calculate_moves(Field[] chess_field)
        {
            List<int> return_list = new List<int>();

            // All moves
            for (int i = -1; i < 2; i++)
            {
                for (int j = -1; j < 2; j++)
                {
                    int new_pos = position + 8 * i + j;

                    if (0 <= new_pos && new_pos < 64 &&
                        position % 8 + j >= 0 && position % 8 + j < 8 &&
                        (!chess_field[new_pos].ContainsPiece() ||
                        chess_field[new_pos].ContainsPiece() &&
                        chess_field[new_pos].GetPiece().getTeamWhite() != team_white))
                    {
                        return_list.Add(new_pos);
                    }
                }
            }

            // Rochade
            if (team_white)
            {
                if (position == 60)
                {
                    if (!chess_field[59].ContainsPiece() &&
                        !chess_field[58].ContainsPiece() &&
                        !chess_field[57].ContainsPiece() &&
                        chess_field[56].GetPiece() is PieceRook &&
                        chess_field[56].GetPiece().getTeamWhite()
                        )
                    {
                        return_list.Add(-58);
                    }
                    if (!chess_field[61].ContainsPiece() &&
                        !chess_field[62].ContainsPiece() &&
                        chess_field[63].GetPiece() is PieceRook &&
                        chess_field[63].GetPiece().getTeamWhite()
                        )
                    {
                        return_list.Add(-62);
                    }
                }
            }
            else
            {
                if (position == 4)
                {
                    if (!chess_field[3].ContainsPiece() &&
                        !chess_field[2].ContainsPiece() &&
                        !chess_field[1].ContainsPiece() &&
                        chess_field[0].GetPiece() is PieceRook &&
                        !chess_field[0].GetPiece().getTeamWhite()
                        )
                    {
                        return_list.Add(-2);
                    }
                    if (!chess_field[5].ContainsPiece() &&
                        !chess_field[6].ContainsPiece() &&
                        chess_field[7].GetPiece() is PieceRook &&
                        !chess_field[7].GetPiece().getTeamWhite()
                        )
                    {
                        return_list.Add(-6);
                    }
                }
            }

            return return_list;
        }

        public override string GetTag()
        {
            return team_white ? "K" : "k";
        }
    }


    struct RochadeComb
    {
        public bool team;
        public string rochade;
        public int position;
        public List<int> in_between_positions;
    }

    struct Move
    {
        public Piece moved_piece;
        public Piece captured_piece;
        public Piece rochade_piece;
        public Piece promotion_piece;
        public string description;
        public int en_passant_before;
        public int gamestate_before;
        public string[] rochade_before;

        public int before;
        public int after;
    }

    class Field
    {
        private bool contains_piece = false;
        private Image image;
        private Image image_target;
        private Image image_moved;
        private bool is_moved = false;
        private Piece piece;

        public Field()
        {

        }

        public Field(Image image_, Image image_target_, Image image_moved_)
        {
            image = image_;
            image_target = image_target_;
            image_moved = image_moved_;
        }

        public void SetMoved(bool is_moved_)
        {
            is_moved = is_moved_;
        }

        public bool GetMoved()
        {
            return is_moved;
        }

        public Piece AssignPiece(Piece piece_)
        {
            Piece temp = piece;
            piece = piece_;
            contains_piece = true;

            return temp;
        }
        public Piece RemovePiece()
        {
            Piece temp = piece;
            piece = null;
            contains_piece = false;

            return temp;
        }

        public bool ContainsPiece()
        {
            return contains_piece;
        }

        public Piece GetPiece()
        {
            if (contains_piece)
            {
                return piece;
            }
            return null;
        }


        public Image GetImage()
        {
            if (contains_piece)
            {
                return is_moved ? piece.getImageMoved() : piece.getImage();
            }
            else
            {
                return is_moved ? image_moved : image;
            }
        }

        public Image GetImageTarget()
        {
            if (contains_piece)
            {
                return piece.getImageTarget();
            }
            else
            {
                return image_target;
            }
        }

        public Image GetImageSelected()
        {
            if (contains_piece)
            {
                return piece.getImageSelected();
            }
            else
            {
                return image;
            }
        }
    }
}
