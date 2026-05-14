# ♟️ ChessInsight

A modular chess analysis application built in C#, designed with a clean separation of concerns across Core logic, Engine, UI, and Tests.

---

## 📁 Project Structure

```
ChessInsight/
├── ChessInsight.Core/      # Domain models, board representation, move logic
├── ChessInsight.Engine/    # Chess engine: evaluation, search algorithms
├── ChessInsight.UI/        # User interface layer
├── ChessInsight.Tests/     # Unit and integration tests
└── ChessInsight.sln        # Visual Studio solution file
```

### ChessInsight.Core
Contains the fundamental building blocks of the chess application:
- Board representation and state management
- Piece definitions and movement rules
- Move generation and validation
- Game logic (check, checkmate, stalemate detection)

### ChessInsight.Engine
Implements chess engine functionality:
- Position evaluation algorithms
- Move search (e.g. Minimax, Alpha-Beta pruning)
- Heuristics and scoring

### ChessInsight.UI
The user-facing layer of the application:
- Interactive chessboard display
- Player input handling
- Game controls and settings

### ChessInsight.Tests
Automated test suite covering:
- Core logic correctness
- Engine evaluation accuracy
- Move generation edge cases

---

## 🚀 Getting Started

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download) (version 6.0 or later recommended)
- [Visual Studio](https://visualstudio.microsoft.com/) or [VS Code](https://code.visualstudio.com/) with C# extension

### Clone the Repository

```bash
git clone https://github.com/AdnaKoss/ChessInsight.git
cd ChessInsight
```

### Build the Solution

```bash
dotnet build ChessInsight.sln
```

### Run the Application

```bash
dotnet run --project ChessInsight.UI
```

### Run Tests

```bash
dotnet test ChessInsight.Tests
```

---

## 🛠️ Technologies

| Technology | Purpose |
|---|---|
| C# / .NET | Primary programming language and runtime |
| Visual Studio Solution | Project organization and build system |
| xUnit / NUnit | Unit testing (see Tests project) |

---


## 👤 Author

**AdnaKoss** — [GitHub Profile](https://github.com/AdnaKoss)
