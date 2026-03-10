# ChessApp: A Chess Learning Experience

**ChessApp** is a sophisticated, mobile-first chess application and learning platform built with **Xamarin.Forms**. It offers a seamless cross-platform experience (Android & iOS) with a powerful custom chess engine at its core.

## Project Overview

ChessApp is designed not just for playing chess, but for **chess-learning**. It leverages custom databases to provide training scenarios and interactive move validation.

- **[ChessApp/](file:///home/lux/projects/chessapp/ChessApp/)**: The heart of the application. Contains XAML UI, cross-platform logic, and the chess engine.
- **[ChessApp.Android/](file:///home/lux/projects/chessapp/ChessApp.Android/)**: Platform-specific implementation for Android.
- **[ChessApp.iOS/](file:///home/lux/projects/chessapp/ChessApp.iOS/)**: Platform-specific implementation for iOS.
- **[ChessEngine.cs](file:///home/lux/projects/chessapp/ChessApp/ChessEngine.cs)**: A custom-built engine managing move generation, rules (castling, en passant, promotion), and gamestate evaluation (check/checkmate).

## Key Features

### 🎓 Chess Learning & Training
- **Training Boards**: Specialized interfaces for practicing specific lines and tactics.
- **Database-Driven Training**: Uses specialized `.txt` databases (found in `ChessApp/data/`) to guide users through learning scenarios.
- **Score Tracking**: Persistent scoring system to track progress during training sessions.

### 🎮 Gameplay & Engine
- **Interactive Chessboard**: Fully responsive board with move highlighting (target, selected, and moved states).
- **Move Validation**: Full support for all FIDE rules, including special moves like en passant and pawn promotion.
- **PGN/FEN Support**: Import and export games using standard formats. Includes clipboard support for easy sharing.
- **Save System**: Automatic game archival to ensure progress is never lost.

### 🎨 Premium Visuals
- **Comprehensive Asset Library**: Over 100 high-quality assets for pieces and board states, ensuring a polished look across all themes.
- **Fluid Animations**: Optimized for mobile performance with responsive UI feedback.

*Developed with focus on performance and educational value.*
