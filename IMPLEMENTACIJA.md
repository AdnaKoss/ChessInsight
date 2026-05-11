# ChessInsight — Kompletna implementacija

> Diplomski rad | WPF šahovska aplikacija s AI analizatorom  
> Framework: .NET 8 · WPF · MVVM · xUnit  
> Autor: AdnaKoss

---

## 1. Arhitektura projekta

```
ChessInsight.sln
├── ChessInsight.Core      — domenske klase (modeli, enumi, generisanje poteza)
├── ChessInsight.Engine    — AI engine (Alpha-Beta, evaluacija, TT)
├── ChessInsight.UI        — WPF aplikacija (MVVM, prikaz, dijalozi)
└── ChessInsight.Tests     — xUnit testovi (31 test)
```

Slojevi su odvojeni po zavisnostima:  
`Core` ← `Engine` ← `UI`  
`Tests` zavisi od `Core` i `Engine` (ne od `UI`).

---

## 2. ChessInsight.Core

### 2.1 Enumi

| Fajl | Sadržaj |
|------|---------|
| `Enums/PieceColor.cs` | `White`, `Black` |
| `Enums/PieceType.cs` | `Pawn, Knight, Bishop, Rook, Queen, King` |
| `Enums/MoveType.cs` | `Normal, Capture, EnPassant, CastleKingside, CastleQueenside, PawnPromotion` |
| `Enums/GameStatus.cs` | `Active, Check, Checkmate, Stalemate, Draw` |

### 2.2 Modeli

#### `Models/Square.cs`
Predstavlja polje na tabli (`Row` 0–7, `Column` 0–7).  
- `FromAlgebraic("e4")` — parsira algebarsku notaciju  
- `IsValid()` — provjera granica  
- `Equals` / `GetHashCode` override

#### `Models/Move.cs`
Nepromjenljiva klasa poteza.  
- Polja: `From`, `To`, `Type`, `PromotionPiece?`  
- `ToString()` — vraća algebarsku notaciju (npr. `e2e4`)

#### `Models/Piece.cs` (apstraktna baza)
- `Color`, `Type`, `Position`  
- Apstraktna metoda: `GetPseudoLegalMoves(Board)` — nefiltrirana pseudo-legalna kretanja  
- `CanAttackSquare(Square, Board)` — koristi se za detekciju šaha

#### Figure (concrete klase)

| Klasa | Kretanje |
|-------|---------|
| `Pawn.cs` | Naprijed 1/2 polja, dijagonalno hvatanje, en passant, promocija |
| `Knight.cs` | L-oblik (±1,±2 / ±2,±1), skače preko figura |
| `Bishop.cs` | Dijagonale, blokira se figurama |
| `Rook.cs` | Horizontale/vertikale, blokira se figurama |
| `Queen.cs` | Kombinacija Topar + Lovac |
| `King.cs` | 1 polje u svim smjerovima + rokada (generiše `CastleKingside`/`CastleQueenside`) |

#### `Models/Board.cs`
Interna reprezentacija: `Piece?[8,8]` matrica.  
- `SetPiece`, `RemovePiece`, `GetPiece`  
- `GetPieces(PieceColor)` — svi komadi zadane boje  
- `GetKing(PieceColor)` — brzo pronalaženje kralja  
- `EnPassantSquare` — polje za en passant hvatanje  
- `Clone()` — duboko kopiranje (koristi Alpha-Beta za immutability)  
- `SetupStartingPosition()` — inicijalna postava

#### `Models/GameState.cs`
Centralna klasa stanja igre.  
- `Board`, `CurrentPlayer`, `Status`  
- Prava na rokadu: `WhiteCanCastleKingside/Queenside`, `BlackCanCastleKingside/Queenside`  
- `HalfMoveClock` (pravilo 50 poteza), `FullMoveNumber`

**Ključne metode:**

```csharp
GameState ApplyMove(Move move)
// Vraća novo stanje (originalno ostaje nepromijenjeno — immutable pattern)
// Handles: normalan potez, hvatanje, en passant, rokada, promocija
// Ažurira: prava rokade, en passant polje, sat polupoteza

GameState ApplyNullMove()
// Prelaz na protivnika BEZ igranog poteza (za Null Move Pruning)
// Briše en passant, čuva sve ostalo

bool IsInCheck(PieceColor color)
bool IsCheckmate(List<Move> legalMoves)
bool IsStalemate(List<Move> legalMoves)
void UpdateStatus(List<Move> legalMoves)

static GameState FromFen(...)  // Factory za FEN parser
GameState Clone()
```

### 2.3 Ostale Core klase

#### `Engine/MoveGenerator.cs`
- `GetLegalMoves(GameState)` — filtrira pseudo-legalne u legalne  
  (primijeni potez → provjeri da li je vlastiti kralj u šahu → odbaci)  
- Generiše sve tipove: normalni, hvatanja, en passant, rokada (uz provjeru prolaznih polja), promocija

#### `FenParser.cs`
- `Parse(string fen)` → `GameState`  
- Parsira sve FEN komponente: postava, boja, rokada, en passant, satovi  
- Baca `ArgumentException` na nevažeći FEN

#### `Zobrist.cs`
Zobrist hashing za transposition table.  
- Fiksni seed (`0x5A3C9B12`) → deterministički hashi  
- `ulong[,,] Pieces[2, 6, 64]` — hash po (boja, tip, polje)  
- `BlackToMove`, `Castling[4]`, `EnPassant[8]`  
- `Compute(GameState)` → `ulong` hash cijele pozicije

---

## 3. ChessInsight.Engine

### 3.1 `Evaluator.cs`
Statička evaluacija pozicije u centipješacima (cp).

| Komponenta | Detalji |
|-----------|---------|
| **Materijal** | P=100, N=320, B=330, R=500, Q=900, K=20000 |
| **Piece-square tablice** | Pozicijske nagrade za svaku figuru po polju (centralna kontrola, kralj sigurnost) |
| **Mobilnost** | Bonus za veći broj legalnih poteza |
| **Endgame detekcija** | Prelaz na endgame tablice za kralja kad nema dama |
| **Perspektiva** | Vraća skor iz perspektive `CurrentPlayer` (pozitivno = bolje za igrača na potezu) |

### 3.2 `AlphaBeta.cs` — Glavni engine

Implementira Alpha-Beta Minimax s iterativnim produbljenjem i svim optimizacijama.

#### Javni API

```csharp
SearchResult FindBestMove(GameState state, int depth,
    CancellationToken ct = default,
    Dictionary<ulong, int>? gameHistory = null)

List<SearchResult> FindTopMovesIterative(GameState state, int maxDepth, int count,
    CancellationToken ct = default,
    Dictionary<ulong, int>? gameHistory = null)
```

#### Implementirane optimizacije

| Optimizacija | Opis | Parametri |
|-------------|------|-----------|
| **Alpha-Beta pruning** | Osnovna rezija grana koje ne mogu biti bolje | α/β bounds |
| **Transposition Table (TT)** | Cache evaluiranih pozicija po Zobrist hashu | 2M unosa (64 MB) |
| **Killer Move Heuristic** | Pamti 2 "ubilačka" poteza po dubini koji su uzrokovali rezije | `Move?[64, 2]` |
| **Null Move Pruning** | Provjeri da li protivnik može škoditi čak i uz slobodan potez | R=2 (adaptivno R=3 na depth>6) |
| **Aspiration Windows** | Pretražuje uzak prozor oko prethodnog skora; širi ako fail | ±50 cp početni, 4× ekspanzija |
| **PVS (Principal Variation Search)** | Null-window pretraga za non-PV poteze, re-search samo pri fail-high | [α, α+1] null window |
| **LMR (Late Move Reductions)** | Smanjuje dubinu za kasne mirne poteze | moveIdx≥4: -1, moveIdx≥8: -2 |
| **Futility Pruning** | Odbacuje mirne poteze koji ne mogu popraviti poziciju | margin 200cp@d1, 500cp@d2 |
| **Iterative Deepening** | Pretražuje dubinu 1..maxDepth, koristi prethodni TT kao move ordering | Dubina 1–6 (UI default) |
| **Move Ordering** | TT best=40000, Promocija=30000, MVV-LVA=10000+, Killer1=9000, Killer2=8000 | Sortiranje prije petlje |
| **Repetition Detection** | Vraća 0 za ponovljenu poziciju (repCount≥2) | gameHistory dict |
| **Cancellation** | Prihvata `CancellationToken`, vraća parcijalni rezultat | Koristi se u UI za stop |

#### Null Move Pruning — uslovi aktivacije
```
depth >= 3  AND  nije u šahu  AND  ply > 0  AND  nije endgame
→ ApplyNullMove() → Search(depth - 1 - R, ...)
→ ako beta cutoff: skor ≥ beta → vrati beta (cut-node)
```

#### LMR + PVS petlja (pseudokod)
```
for each move (ordered):
    apply move → nextState
    if searchedCount == 0:
        score = Search(depth-1, full window)   // PV potez
    else:
        reduction = isQuiet && depth>=3 && moveIdx>=4 && !inCheck
                    ? (moveIdx>=8 ? 2 : 1) : 0
        lmrDepth = max(0, depth-1-reduction)
        score = Search(lmrDepth, [α, α+1])     // null window
        if score > α:
            score = Search(depth-1, [α, β])    // re-search
    update bestScore, alpha, bestMove
```

### 3.3 `TranspositionTable.cs`
- Veličina: `1 << 21` = 2,097,152 unosa  
- `TtEntry` struct: `Hash, Score, Depth, Flag(Exact/LowerBound/UpperBound), BestMove?`  
- `TryProbe(hash, depth, α, β)` — vraća `bestMove` i za TT promašaje (za move ordering)  
- `Store(hash, depth, score, flag, bestMove)` — zamjena po dubini (deeper-is-better)

### 3.4 `Minimax.cs`
Osnovna Minimax implementacija (bez optimizacija). Koristi se isključivo za benchmark poređenje u testovima.

---

## 4. ChessInsight.UI

### 4.1 XAML fajlovi

| Fajl | Uloga |
|------|-------|
| `App.xaml` | Globalni resursi: Carbon & Ice paleta, stilovi dugmadi, input |
| `MainWindow.xaml` | Layout C: 4-redni raspored (Topbar / Tabla / 4 kartice / Bottombar) |
| `Views/BoardView.xaml` | 8×8 UniformGrid s dugmadima za polja; SVG + Unicode figura |
| `Views/PromotionDialog.xaml` | Dijalog za izbor figure pri promociji pješaka |
| `Views/PositionEditorDialog.xaml` | Editor pozicije s drag-and-drop po tabli |

#### Carbon & Ice paleta (App.xaml)

| Varijabla | Boja | Upotreba |
|----------|------|---------|
| `BgApp` | `#141414` | Pozadina prozora |
| `BgPanel` | `#1C1C1C` | Topbar, bottombar |
| `BgCard` | `#242424` | Kartice s analizom |
| `TextPrimary` | `#E8E8E8` | Glavni tekst |
| `TextSecondary` | `#888780` | Sekundarni tekst, labele |
| `TextAccent` | `#85B7EB` | Logo, naglašeni skor |
| `BorderColor` | `#14FFFFFF` | Suptilne granice (8% bijele) |
| `SquareLight` | `#BABABA` | Svijetla polja table |
| `SquareDark` | `#3A3A3A` | Tamna polja table |
| `MoveColorBest` | `#1D9E75` | Najbolji potez (zelena) |
| `MoveColorLegal` | `#C8A84B` | Legalni potezi |
| `MoveColorBlunder` | `#B83232` | Blunder (??) |
| `MoveColorError` | `#D85A30` | Greška (?) |
| `MoveColorMistake` | `#EF9F27` | Netačnost (?!) |
| `MoveColorInaccuracy` | `#85B7EB` | Slabost (!?) |

#### MainWindow.xaml — Layout C

```
┌─────────────────────────────────────────────────────┐
│ Row 0 │ ♟ ChessInsight  │  FEN: [___________]  Učitaj│  ← Topbar (52px)
├─────────────────────────────────────────────────────┤
│                                                     │
│ Row 1 │          ┌────────────┐                     │  ← Tabla centrirana (*)
│       │          │  8×8 tabla │                     │
│       │          └────────────┘                     │
│       │          [● Bijeli na potezu]               │
├─────────────────────────────────────────────────────┤
│ Row 2 │ Evaluacija │ Top 3 poteza ││ Statistike │Istorija│  ← 4 kartice (Auto)
├─────────────────────────────────────────────────────┤
│ Row 3 │ ◀Nazad  Naprijed▶  ⟳Okreni  ↺Resetuj  ✎Postavi│[ANALIZIRAJ]│  ← Bottombar (52px)
└─────────────────────────────────────────────────────┘
```

### 4.2 ViewModels

#### `BoardViewModel.cs` — 665 linija
Centralna klasa koja povezuje engine s prikazom.

**Stanje i navigacija:**
```csharp
GameState _gameState              // Trenutno stanje igre
List<GameState> _stateHistory     // Historija za navigaciju
List<(Move, string san, ...)> _moveLog
int _viewIndex                    // Trenutna pozicija u historiji
```

**Veza s engineom:**
```csharp
private readonly AlphaBeta _engine = new();
private readonly MoveGenerator _generator = new();
private const int AnalysisDepth = 6;
```

**Automatska analiza (continuous analysis):**
```csharp
public void ToggleAutoAnalysis()
// Pokreće/zaustavlja pozadinski Task koji:
// 1. Čeka kraj prethodne analize
// 2. Poziva engine.FindTopMovesIterative(state, depth:6, count:3, ct)
// 3. Ažurira ScoreText, Move1-3Text, Score1-3Text, DepthText, NodesText, TimeText
// 4. Ponavlja pri svakom potezu igrača
```

**Konekcija engine → UI (update metoda):**
```csharp
private void UpdateAnalysisPanel(List<SearchResult> results, long ms)
// Konvertuje engine SearchResult u tekstualne bindings:
// ScoreText   = "+1.23" / "-0.45" / "M3" (mat)
// Move1-3Text = SAN notacija poteza
// Score1-3Text = score u cp
// HighlightBestMove() → obojen kvadrat (#1D9E75) na tabli
```

**Potezi igrača:**
```csharp
public void SelectSquare(int index)
// Klik na polje → odabir figure → prikaz legalnih poteza (BrLegal)
// Klik na legalno polje → ApplyPlayerMove()

private async Task ApplyPlayerMove(Move move)
// ApplyMove → RefreshBoard → AddMoveToHistory → TriggerAnalysis
```

**Drag-and-drop (BoardView.xaml.cs):**
```csharp
Board_PreviewMouseDown  // Uzmi figuru
Board_PreviewMouseMove  // PieceDragAdorner prati kursor
Board_PreviewMouseUp    // Ispusti → SelectSquare(to)
```

**Promocija:**
```csharp
public event Func<PieceColor, PieceType>? PromotionRequired;
// ViewModel pali event → MainWindow.xaml.cs otvori PromotionDialog
// Vraća PieceType.Queen/Rook/Bishop/Knight
```

**Brush-evi ploče (Carbon & Ice):**
```csharp
BrLight    = #BABABA   // svijetlo polje
BrDark     = #3A3A3A   // tamno polje
BrLegal    = #C8A84B   // legalni potez (kružić)
BrBest     = #1D9E75   // highlight najboljeg poteza
BrSelected = #85B7EB   // odabrana figura
BrWhite    = #FAFAFA   // boja bijelih figura (unicode)
BrBlack    = #161616   // boja crnih figura (unicode)
```

#### `SquareViewModel.cs`
ObservableObject za jedno polje: `Background`, `PieceSymbol`, `PieceSvgUri`, `PieceColor`, `PieceMargin`, `IsLegalMove`, `IsLegalCapture`, `Row`, `Column`, `Index`.

#### `MoveHistoryEntry.cs`
`Number`, `WhiteMove`, `BlackMove` — prikazuje se u kartici "Istorija poteza".

### 4.3 Ostale UI klase

| Klasa | Uloga |
|-------|-------|
| `BoardView.xaml.cs` | Klik-handler + drag-and-drop logika, SVG rendering |
| `PieceDragAdorner.cs` | WPF Adorner koji "vuče" figuru uz kursor |
| `PositionEditorDialog.xaml.cs` | Kompletni editor: drag-and-drop figura, brisanje, promjena boje na potezu, FEN output |
| `PromotionDialog.xaml.cs` | Izbor figure pri promociji pješaka |
| `NullToVisibilityConverter.cs` | `null → Collapsed`, `non-null → Visible` (za SVG/Unicode prebacivanje) |

### 4.4 SVG figure
U `Resources/Pieces/` — po konvenciji `wp.svg`, `bp.svg`, `wn.svg` itd.  
SharpVectors.Reloaded renderuje SVG direktno u WPF bez rasterizacije.  
Fallback: Unicode simboli (♙♘♗♖♕♔♟♞♝♜♛♚) ako SVG fajl nije pronađen.

---

## 5. ChessInsight.Tests

Ukupno **31 test** u 6 klasa, sve prolaze.

### Pregled testova

| Klasa | Testovi | Šta testira |
|-------|---------|-------------|
| `SquareTests` | 4 | `FromAlgebraic`, `IsValid`, `Equals` |
| `KnightTests` | 5 | L-kretanja, granice, blokiranje |
| `RookTests` | 6 | Horizontale/vertikale, blokiranje figurama |
| `MoveGeneratorTests` | 5 | Počelna pozicija (20 poteza), šah blokiranje, pat |
| `MinimaxTests` | 11 | Performanse, mat, besplatna dama, AlphaBeta vs Minimax benchmark |
| `EngineOptimizationTests` | 14 | Sve nove optimizacije (vidi dolje) |

### EngineOptimizationTests (14 testova)

| Test | Provjera |
|------|---------|
| `ApplyNullMove_SwitchesCurrentPlayer` | Null potez mijenja igrača |
| `ApplyNullMove_BlackSwitchesToWhite` | Dvostruki null → isti igrač |
| `ApplyNullMove_PreservesCastlingRights` | Rokada prava ostaju |
| `ApplyNullMove_ClearsEnPassantSquare` | En passant polje se briše |
| `ApplyNullMove_DoesNotModifyBoard` | Broj figura ostaje isti |
| `FindTopMovesIterative_Depth6_ReturnsTopMoves` | Iterativno produbljivanje dubina 6 |
| `FindTopMovesIterative_Cancellation_ReturnsPartialResult` | Otkazivanje vraća djelomičan rezultat |
| `AlphaBeta_FindsMateInTwo` | Scholar's mate pozicija → skor >90000 |
| `AlphaBeta_CapturesFreeQueenImmediately` | Besplatna dama → engine je uzima |
| `AlphaBeta_AvoidsBlunderingQueen` | Engine ne žrtvuje damu za topa |
| `Performance_AlphaBeta_AllOptimizations_Depth5` | Dubina 5 — mjerenje čvorova i vremena |
| `Performance_IterativeDeepening_Vs_DirectSearch` | Usporedba iterativnog vs. direktnog |
| `AlphaBeta_StartingPosition_ReturnsMoveWithin10Seconds` | Dubina 6 u roku od 10s |
| `AlphaBeta_EmptyHistoryDoesNotCrash` | Prazna historija ne uzrokuje crash |

---

## 6. Git historija

| Commit | Poruka |
|--------|--------|
| `0fa3e2b` | Add project files |
| `31af484` | Initial implementation — Core, Engine, Tests |
| `cb10122` | Add WPF UI — BoardView, BoardViewModel, MainWindow, App |
| `a48e057` | Add FEN loader, game-over detection, promotion dialog, move history, and board label flip |
| `c9d5231` | Add drag-and-drop piece movement and fix Bosnian SAN notation |
| `4c70717` | Add SVG pieces, drag-and-drop, continuous analysis, SAN notation fixes |
| `f2bc9ee` | Improve UX, evaluator accuracy, and add move navigation |
| `55e6fe2` | Add engine optimizations, position editor, and engine tests |
| `64baaf2` | Apply Carbon & Ice dark theme and Layout C redesign |

---

## 7. NuGet paketi

| Paket | Verzija | Projekat | Uloga |
|-------|---------|---------|-------|
| `CommunityToolkit.Mvvm` | 8.4.2 | UI | `[ObservableProperty]`, `ObservableObject` |
| `SharpVectors.Reloaded` | 1.8.5 | UI | SVG rendering u WPF |
| `xunit` | 2.9.3 | Tests | Test framework |
| `xunit.runner.visualstudio` | 2.8.2 | Tests | VS integracija |
| `Microsoft.NET.Test.Sdk` | 17.12.0 | Tests | .NET test runner |

---

## 8. Sažetak — šta je implementirano

### Core
- [x] Kompletna šahovska logika (sve figure, sva specijalna kretanja)
- [x] Immutable GameState pattern (svaki potez vraća novi objekat)
- [x] FEN parser (čitanje i postavljanje pozicija)
- [x] Zobrist hashing (64-bitni hash svake pozicije)
- [x] Generisanje legalnih poteza s filterom šaha

### Engine
- [x] Minimax (bazna verzija, za benchmark)
- [x] Alpha-Beta pruning
- [x] Transposition Table (2M unosa, Zobrist keš)
- [x] Killer Move Heuristic (2 killera po dubini)
- [x] Null Move Pruning (adaptivni R=2/3)
- [x] Aspiration Windows (±50cp, 4× ekspanzija)
- [x] Principal Variation Search (null-window + re-search)
- [x] Late Move Reductions (−1 na idx≥4, −2 na idx≥8)
- [x] Futility Pruning (200cp@d1, 500cp@d2)
- [x] Iterativno produbljivanje (1→maxDepth, TT warm-start)
- [x] Cancellation (CancellationToken, parcijalni rezultat)
- [x] Piece-square tablice za evaluaciju

### UI
- [x] WPF MVVM (CommunityToolkit)
- [x] 8×8 tabla s klik i drag-and-drop
- [x] SVG figure (SharpVectors) s Unicode fallbackom
- [x] Prikaz legalnih poteza (kružić / prsten)
- [x] Highlight najboljeg poteza (#1D9E75)
- [x] Kontinuirana analiza (background Task, CancellationToken)
- [x] Navigacija kroz historiju poteza (◀/▶, tipke ←→)
- [x] FEN učitavanje (parser + validacija)
- [x] Promocija pješaka (dijalog)
- [x] Editor pozicije (drag-and-drop u dijalogu)
- [x] Carbon & Ice dark tema (Layout C)
- [x] Prikaz top 3 poteza s skorovima
- [x] Statistike analize (dubina, čvorovi, vrijeme)

### Testovi
- [x] 31 test, svi prolaze
- [x] Pokrivenost: figure, generator, Minimax, Alpha-Beta, sve optimizacije
