using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ChessInsight.Core.Enums;
using ChessInsight.Core.Models;
using SharpVectors.Converters;

namespace ChessInsight.UI.Views
{
    public partial class PositionEditorDialog : Window
    {
        // Stanje ploče: [row, col] gdje row 0 = rank 1 (dno za bijele)
        private readonly Piece?[,] _board   = new Piece?[8, 8];
        private readonly Border[,] _squares = new Border[8, 8];

        // Trenutna selekcija u paleti
        private PieceType  _selectedType  = PieceType.Queen;
        private PieceColor _selectedColor = PieceColor.White;
        private bool       _isEraser      = false;
        private Button?    _activePaletteBtn;
        private Point      _paletteMouseDownPos;

        // Boje polja
        private static readonly Brush BrLight = new SolidColorBrush(Color.FromRgb(0xF0, 0xD9, 0xB5));
        private static readonly Brush BrDark  = new SolidColorBrush(Color.FromRgb(0xB5, 0x88, 0x63));

        public string? ResultFen { get; private set; }

        private static readonly (PieceType type, string wSym, string bSym, string name)[] PieceData =
        {
            (PieceType.King,   "♔", "♚", "Kralj"),
            (PieceType.Queen,  "♕", "♛", "Dama"),
            (PieceType.Rook,   "♖", "♜", "Top"),
            (PieceType.Bishop, "♗", "♝", "Lovac"),
            (PieceType.Knight, "♘", "♞", "Konj"),
            (PieceType.Pawn,   "♙", "♟", "Pješak"),
        };

        public PositionEditorDialog()
        {
            InitializeComponent();
            BuildBoardSquares();
            BuildPalette(WhitePalette, PieceColor.White);
            BuildPalette(BlackPalette, PieceColor.Black);
            UpdateSelectedLabel();
        }

        // Učitaj trenutnu poziciju s main ploče
        public void LoadCurrentPosition(Board board, PieceColor sideToMove)
        {
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    _board[r, c] = board.GetPiece(new Square(r, c));
            RbWhite.IsChecked = sideToMove == PieceColor.White;
            RbBlack.IsChecked = sideToMove == PieceColor.Black;
            RefreshBoard();
        }

        // ── Izgradnja ploče ──────────────────────────────────────

        private void BuildBoardSquares()
        {
            for (int vRow = 0; vRow < 8; vRow++) // vizualni red: 0 = vrh = rank 8
            {
                for (int col = 0; col < 8; col++)
                {
                    int boardRow = 7 - vRow;
                    bool isLight = (boardRow + col) % 2 == 1;

                    var highlight = new Border
                    {
                        Background = Brushes.Transparent,
                        IsHitTestVisible = false,
                    };

                    var svgView = new SvgViewbox
                    {
                        Margin = new Thickness(6),
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment   = VerticalAlignment.Center,
                        IsHitTestVisible    = false,
                    };

                    var innerGrid = new Grid();
                    innerGrid.Children.Add(highlight);
                    innerGrid.Children.Add(svgView);

                    var square = new Border
                    {
                        Background = isLight ? BrLight : BrDark,
                        Cursor     = Cursors.Hand,
                        Tag        = (boardRow, col),
                        Child      = innerGrid,
                        AllowDrop  = true,
                    };

                    square.MouseLeftButtonDown += Square_MouseDown;
                    square.MouseEnter += (_, _) =>
                        highlight.Background = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0));
                    square.MouseLeave += (_, _) =>
                        highlight.Background = Brushes.Transparent;

                    // Drop iz palete (drag & drop)
                    square.DragEnter += (s, e) =>
                    {
                        if (e.Data.GetDataPresent("ChessPiece"))
                        {
                            e.Effects = DragDropEffects.Copy;
                            highlight.Background = new SolidColorBrush(Color.FromArgb(100, 0x4C, 0xAF, 0x50));
                        }
                        e.Handled = true;
                    };
                    square.DragLeave += (_, _) =>
                        highlight.Background = Brushes.Transparent;

                    square.Drop += (s, e) =>
                    {
                        highlight.Background = Brushes.Transparent;
                        if (e.Data.GetData("ChessPiece") is ValueTuple<PieceType, PieceColor> pd)
                        {
                            var (row2, col2) = ((int, int))((Border)s).Tag;
                            _board[row2, col2] = CreatePiece(pd.Item1, pd.Item2, new Square(row2, col2));
                            RefreshSquare(row2, col2);
                        }
                        e.Handled = true;
                    };

                    EditorBoardGrid.Children.Add(square);
                    _squares[boardRow, col] = square;
                }
            }
        }

        // ── Paleta figura ────────────────────────────────────────

        private void BuildPalette(UniformGrid grid, PieceColor color)
        {
            foreach (var (type, wSym, bSym, name) in PieceData)
            {
                var symbol = color == PieceColor.White ? wSym : bSym;
                var captured = (type, color); // capture za lambda

                var btn = new Button
                {
                    Content = new TextBlock
                    {
                        Text = symbol,
                        FontFamily = new FontFamily("Segoe UI Symbol"),
                        FontSize = 18,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment   = VerticalAlignment.Center,
                    },
                    Background      = new SolidColorBrush(Color.FromRgb(0x43, 0x46, 0x4D)),
                    BorderBrush     = new SolidColorBrush(Color.FromRgb(0x55, 0x58, 0x60)),
                    BorderThickness = new Thickness(2),
                    Cursor          = Cursors.Hand,
                    Margin          = new Thickness(2),
                    Padding         = new Thickness(2),
                    ToolTip         = $"{name} ({(color == PieceColor.White ? "bijela" : "crna")})",
                    Template        = BuildPaletteBtnTemplate(),
                };

                // Klik = selektuj figuru
                btn.Click += (s, _) => SelectPalettePiece(captured.type, captured.color, btn);

                // Drag = direktno postavljanje na ploču
                btn.PreviewMouseLeftButtonDown += (_, e) =>
                    _paletteMouseDownPos = e.GetPosition(null);

                btn.PreviewMouseMove += (_, e) =>
                {
                    if (e.LeftButton != MouseButtonState.Pressed) return;
                    var pos = e.GetPosition(null);
                    if (Math.Abs(pos.X - _paletteMouseDownPos.X) > SystemParameters.MinimumHorizontalDragDistance ||
                        Math.Abs(pos.Y - _paletteMouseDownPos.Y) > SystemParameters.MinimumVerticalDragDistance)
                    {
                        SelectPalettePiece(captured.type, captured.color, btn);
                        var data = new DataObject("ChessPiece",
                            (ValueTuple<PieceType, PieceColor>)(captured.type, captured.color));
                        DragDrop.DoDragDrop(btn, data, DragDropEffects.Copy);
                        e.Handled = true;
                    }
                };

                grid.Children.Add(btn);

                // Početna selekcija: bijela dama
                if (color == PieceColor.White && type == PieceType.Queen)
                    SelectPalettePiece(type, color, btn);
            }
        }

        private static ControlTemplate BuildPaletteBtnTemplate()
        {
            var template = new ControlTemplate(typeof(Button));
            var borderFact = new FrameworkElementFactory(typeof(Border));

            var bgBinding = new System.Windows.Data.Binding("Background")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent)
            };
            var bbBinding = new System.Windows.Data.Binding("BorderBrush")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent)
            };
            var btBinding = new System.Windows.Data.Binding("BorderThickness")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(
                    System.Windows.Data.RelativeSourceMode.TemplatedParent)
            };

            borderFact.SetBinding(Border.BackgroundProperty,      bgBinding);
            borderFact.SetBinding(Border.BorderBrushProperty,     bbBinding);
            borderFact.SetBinding(Border.BorderThicknessProperty, btBinding);
            borderFact.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));

            var cpFact = new FrameworkElementFactory(typeof(ContentPresenter));
            cpFact.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cpFact.SetValue(ContentPresenter.VerticalAlignmentProperty,   VerticalAlignment.Center);
            borderFact.AppendChild(cpFact);
            template.VisualTree = borderFact;
            return template;
        }

        // ── Selekcija ────────────────────────────────────────────

        private void SelectPalettePiece(PieceType type, PieceColor color, Button btn)
        {
            _selectedType  = type;
            _selectedColor = color;
            _isEraser      = false;

            if (_activePaletteBtn != null)
                _activePaletteBtn.BorderBrush =
                    new SolidColorBrush(Color.FromRgb(0x55, 0x58, 0x60));

            _activePaletteBtn = btn;
            btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));

            UpdateSelectedLabel();
        }

        private void UpdateSelectedLabel()
        {
            if (_isEraser)
            {
                TxtSelected.Text       = "✕ Gumica";
                TxtSelected.Foreground = new SolidColorBrush(Color.FromRgb(0xE0, 0x7B, 0x39));
                return;
            }
            var info      = Array.Find(PieceData, p => p.type == _selectedType);
            var sym       = _selectedColor == PieceColor.White ? info.wSym : info.bSym;
            var colorName = _selectedColor == PieceColor.White ? "bijela" : "crna";
            TxtSelected.Text       = $"{sym} {info.name} ({colorName})";
            TxtSelected.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
        }

        // ── Interakcija s poljem ─────────────────────────────────

        private void Square_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var border = (Border)sender;
            var (row, col) = ((int, int))border.Tag;

            if (_isEraser)
            {
                _board[row, col] = null;
                RefreshSquare(row, col);
                return;
            }

            // Toggle: ista figura → ukloni; drugačija/prazno → postavi
            var existing = _board[row, col];
            if (existing != null && existing.Type == _selectedType && existing.Color == _selectedColor)
                _board[row, col] = null;
            else
                _board[row, col] = CreatePiece(_selectedType, _selectedColor, new Square(row, col));

            RefreshSquare(row, col);
        }

        // ── Osvježavanje prikaza ─────────────────────────────────

        private void RefreshBoard()
        {
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    RefreshSquare(r, c);
        }

        private void RefreshSquare(int row, int col)
        {
            var innerGrid = (Grid)_squares[row, col].Child;
            var svgView   = (SvgViewbox)innerGrid.Children[1];
            var piece     = _board[row, col];

            if (piece == null)
            {
                svgView.Source = null;
                return;
            }

            svgView.Margin = piece.Type switch
            {
                PieceType.Pawn   => new Thickness(10),
                PieceType.Rook   => new Thickness(9),
                PieceType.Knight => new Thickness(9),
                _                => new Thickness(6),
            };

            var c = piece.Color == PieceColor.White ? 'w' : 'b';
            var t = piece.Type switch
            {
                PieceType.King   => 'K',
                PieceType.Queen  => 'Q',
                PieceType.Rook   => 'R',
                PieceType.Bishop => 'B',
                PieceType.Knight => 'N',
                PieceType.Pawn   => 'P',
                _ => '?'
            };
            svgView.Source = new Uri(
                $"pack://application:,,,/ChessInsight;component/Resources/Pieces/{c}{t}.svg",
                UriKind.Absolute);
        }

        // ── Pomoćne metode ───────────────────────────────────────

        private static Piece CreatePiece(PieceType type, PieceColor color, Square pos) => type switch
        {
            PieceType.King   => (Piece)new King(color, pos),
            PieceType.Queen  => new Queen(color, pos),
            PieceType.Rook   => new Rook(color, pos),
            PieceType.Bishop => new Bishop(color, pos),
            PieceType.Knight => new Knight(color, pos),
            PieceType.Pawn   => new Pawn(color, pos),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        // ── Handleri dugmadi ─────────────────────────────────────

        private void SideToMove_Checked(object sender, RoutedEventArgs e) { }

        private void BtnEraser_Click(object sender, RoutedEventArgs e)
        {
            _isEraser = true;
            if (_activePaletteBtn != null)
                _activePaletteBtn.BorderBrush =
                    new SolidColorBrush(Color.FromRgb(0x55, 0x58, 0x60));
            _activePaletteBtn = null;
            BtnEraser.BorderBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
            UpdateSelectedLabel();
        }

        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                    _board[r, c] = null;
            RefreshBoard();
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            var fen = GenerateFen();
            if (fen == null)
            {
                MessageBox.Show(
                    "Pozicija nije validna.\n\nSvaka strana mora imati tačno jednog kralja.",
                    "Nevažeća pozicija",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
            ResultFen    = fen;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ── Generisanje FEN-a ────────────────────────────────────

        private string? GenerateFen()
        {
            // Validacija: oba kralja moraju postojati
            bool hasWhiteKing = false, hasBlackKing = false;
            for (int r = 0; r < 8; r++)
                for (int c = 0; c < 8; c++)
                {
                    if (_board[r, c] is { Type: PieceType.King, Color: PieceColor.White }) hasWhiteKing = true;
                    if (_board[r, c] is { Type: PieceType.King, Color: PieceColor.Black }) hasBlackKing = true;
                }
            if (!hasWhiteKing || !hasBlackKing) return null;

            var sb = new StringBuilder();

            // FEN ide od rank 8 (row 7) prema rank 1 (row 0)
            for (int r = 7; r >= 0; r--)
            {
                int empty = 0;
                for (int c = 0; c < 8; c++)
                {
                    var piece = _board[r, c];
                    if (piece == null)
                    {
                        empty++;
                    }
                    else
                    {
                        if (empty > 0) { sb.Append(empty); empty = 0; }
                        sb.Append(GetFenChar(piece));
                    }
                }
                if (empty > 0) sb.Append(empty);
                if (r > 0) sb.Append('/');
            }

            // Ko je na potezu
            sb.Append(RbWhite.IsChecked == true ? " w" : " b");

            // Rokada, en passant, sat (defaulti za custom poziciju)
            sb.Append(" - - 0 1");

            return sb.ToString();
        }

        private static char GetFenChar(Piece piece)
        {
            var ch = piece.Type switch
            {
                PieceType.King   => 'K',
                PieceType.Queen  => 'Q',
                PieceType.Rook   => 'R',
                PieceType.Bishop => 'B',
                PieceType.Knight => 'N',
                PieceType.Pawn   => 'P',
                _ => '?'
            };
            return piece.Color == PieceColor.Black ? char.ToLower(ch) : ch;
        }
    }
}
