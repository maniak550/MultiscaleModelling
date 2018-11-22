using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Linq;

namespace CA
{
    public partial class CAForm : Form
    {
        class GridPanel : Panel
        {
            public GridPanel()
            {
                DoubleBuffered = true;
            }
        }

        private const int GRID_DIM = 300;
        private const int CELL_SIZE = 2;

        private Random random = new Random();

        private GridPanel gridPanel = new GridPanel();
        private Cell[,] grid = null;

        private int numberOfGrains = 5;
        private int numberOfInclusions = 5;
        private int sizeOfInclusion = 2;
        private Timer timer = null;

        List<Point> vonNeumannDirections = new List<Point>
        {
            new Point(-1, 0),
            new Point(1, 0),
            new Point(0, -1),
            new Point(0, 1),
        };

        List<Point> mooreDirections = new List<Point>
        {
            new Point(-1, 0),
            new Point(1, 0),
            new Point(0, -1),
            new Point(0, 1),
            new Point(-1, -1),
            new Point(-1, 1),
            new Point(1, -1),
            new Point(1, 1)
        };

        public CAForm()
        {
            InitializeComponent();
            Init();
        }

        public void Init()
        {
            Controls.Add(gridPanel);
            gridPanel.Size = new Size(600, 600);
            gridPanel.BackColor = Color.Black;
            gridPanel.Paint += GridPanel_Paint;

            grid = new Cell[GRID_DIM, GRID_DIM];
            for (int i = 0; i < GRID_DIM; i++)
            {
                for (int j = 0; j < GRID_DIM; j++)
                {
                    grid[i, j] = new Cell();
                }
            }

            neighbourhoodTypeComboBox.SelectedIndex = 0;
            shapeOfInclusionComboBox.SelectedIndex = 0;
            timeOfCreationOfInclusionsComboBox.SelectedIndex = 0;

            Reset();
        }

        // Poczatkowa inicjalizacja ziaren
        public void InitGrains()
        {
            int numberOfGrainsAdded = 0;

            while (numberOfGrainsAdded < numberOfGrains)
            {
                int x = random.Next(GRID_DIM);
                int y = random.Next(GRID_DIM);

                if (grid[x, y].type != CellType.EMPTY)
                {
                    continue;
                }

                Color color = RandomColor();

                if (InsideGrid(x, y))
                {
                    grid[x, y] = new Cell(color, CellType.GRAIN);
                }
                numberOfGrainsAdded++;
            }
        }

        // Inicjalizacja wtracen
        // onGrainBoundrary - czy wtracenia maja sie tworzyc na granicach ziaren
        public void InitInclusions(bool onGrainBoundrary = false)
        {
            int numberOfInclusionsAdded = 0;

            int tries = 0;
            int MAX_TRIES = 10000;

            while (numberOfInclusionsAdded < numberOfInclusions)
            {
                int x;
                int y;

                if (onGrainBoundrary)
                {
                    List<Point> pointsOnGrainBoundrary = PointsOnGrainBoundrary();
                    Point p = pointsOnGrainBoundrary[random.Next(pointsOnGrainBoundrary.Count)];
                    x = p.X;
                    y = p.Y;
                }
                else
                {
                    x = random.Next(sizeOfInclusion, GRID_DIM - sizeOfInclusion);
                    y = random.Next(sizeOfInclusion, GRID_DIM - sizeOfInclusion);
                }
                if (shapeOfInclusionComboBox.SelectedIndex == 0)
                {
                    if (InitInclusion(x, y, Shape.SQUARE))
                    {
                        numberOfInclusionsAdded++;
                    }
                }
                else if (shapeOfInclusionComboBox.SelectedIndex == 1)
                {
                    if (InitInclusion(x, y, Shape.CIRCULAR))
                    {
                        numberOfInclusionsAdded++;
                    }
                }
                tries++;
                if (tries == MAX_TRIES)
                {
                    MessageBox.Show("Can't init inclusions!");
                }
            }
        }

        // Utworz wtracenie pojedyczne wtracenie o zadanym ksztalcie
        private bool InitInclusion(int x, int y, Shape shape)
        {
            Color color = RandomColor();
            int r = sizeOfInclusion;

            for (int dx = -sizeOfInclusion; dx <= sizeOfInclusion; dx++)
            {
                for (int dy = -sizeOfInclusion; dy <= sizeOfInclusion; dy++)
                {
                    int nx = x + dx;
                    int ny = y + dy;

                    if (!InsideGrid(nx, ny) || grid[nx, ny].type == CellType.INCLUSION)
                    {
                        if (shape == Shape.SQUARE)
                        {
                            return false;
                        }
                        if (shape == Shape.CIRCULAR && dx * dx + dy * dy <= r * r)
                        {
                            return false;
                        }
                    }                   
                }
            }

            for (int dx = -sizeOfInclusion; dx <= sizeOfInclusion; dx++)
            {
                for (int dy = -sizeOfInclusion; dy <= sizeOfInclusion; dy++)
                {
                    int nx = x + dx;
                    int ny = y + dy;

                    if (shape == Shape.SQUARE)
                    {
                        grid[nx, ny] = new Cell(color, CellType.INCLUSION);
                    }
                    if (shape == Shape.CIRCULAR && dx * dx + dy * dy <= r * r)
                    {
                        grid[nx, ny] = new Cell(color, CellType.INCLUSION);
                    }
                }
            }
            return true;
        }

        // Krok (klatka) symulacja
        private void SimulationStep()
        {
            // dopoki wszystkie pola nie za wypelnione
            if (!IsGridFull())
            {
                // wykonuj rozrost ziaren
                GrowGrains();
            }
            else // jesli wszystkie pola sa wypelnione
            {
                // jesli wtracenia maja sie tworzyc na koncu symulacji 
                // to je utworz
                if (timeOfCreationOfInclusionsComboBox.SelectedIndex == 1)
                {
                    InitInclusions(true);
                }
                // zakoncz rozrost ziaren
                GrainGrowthEnd();
            }
            gridPanel.Refresh();
        }

        // czy wszystkie pola sa zajete
        bool IsGridFull()
        {
            for (int i = 0; i < GRID_DIM; i++)
            {
                for (int j = 0; j < GRID_DIM; j++)
                {
                    if (grid[i, j].type == CellType.EMPTY)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        Color MostCommonNeighbourColor(int x, int y)
        {         
            Dictionary<Color, int> d = new Dictionary<Color, int>();

            foreach (Point p in Neighbours(x, y))
            {
                if (grid[p.X, p.Y].type == CellType.GRAIN)
                {
                    Color color = grid[p.X, p.Y].color;

                    if (color == Color.Black)
                    {
                        continue;
                    }
                    if (!d.ContainsKey(color))
                    {
                        d[color] = 0;
                    }
                    d[color]++;
                }
            }
            if (d.Count > 0)
            {
                Color result = d.OrderByDescending(p => p.Value).First().Key;
                return result;
            }
            else
            {
                return Color.Black;
            }
        }

        // wzrost ziaren
        void GrowGrains()
        {
            Cell[,] gridCopy = CopyGrid(grid);
            for (int x = 0; x < GRID_DIM; x++)
            {
                for (int y = 0; y < GRID_DIM; y++)
                {
                    if (InsideGrid(x, y) && grid[x, y].type == CellType.EMPTY)
                    {
                        Color color = MostCommonNeighbourColor(x, y);
                        if (color != Color.Black)
                        {
                            gridCopy[x, y].color = color;
                            gridCopy[x, y].type = CellType.GRAIN;
                        }
                    }
                }
            }
            
            grid = CopyGrid(gridCopy);
            Refresh();
        }

        // czy dane pole moze sie rozrastac (= czy jest ziarnem)
        private bool CanGrow(int i, int j)
        {
            return grid[i, j].type == CellType.GRAIN;
        }

        // wyznacz liste sasiadow wzgledem sasiedztwa vonNeumann albo Moore
        private List<Point> Neighbours(int i, int j)
        {   
            if (neighbourhoodTypeComboBox.SelectedIndex == 0)
            {
                return Neighbours(i, j, vonNeumannDirections);
            }
            else
            {
                return Neighbours(i, j, mooreDirections);
            }
        }

        // wyznacz liste sasiadow danego pola
        private List<Point> Neighbours(int x, int y, List<Point> directions)
        {
            List<Point> neighbours = new List<Point>();

            foreach (Point p in directions)
            {
                int nx = x + p.X;
                int ny = y + p.Y;
                if (InsideGrid(nx, ny))
                {
                    neighbours.Add(new Point(nx, ny));
                }
            }
            return neighbours;
        }

        // czy pole jest na planszy
        private bool InsideGrid(int x, int y)
        {
            return x >= 0 && x < GRID_DIM && y >= 0 && y < GRID_DIM;
        }

        // zakoncz rozrost (= zatrzymaj timer)
        void GrainGrowthEnd()
        {
            timer.Stop();
            timer = null;
        }

        // przekopiuj plansze
        Cell[,] CopyGrid(Cell[, ] a)
        {
            Cell[, ] b = new Cell[GRID_DIM, GRID_DIM];

            for (int i = 0; i < GRID_DIM; i++)
            {
                for (int j = 0; j < GRID_DIM; j++)
                {
                    b[i, j] = new Cell(a[i, j].color, a[i, j].type);
                }
            }

            return b;
        }

        // wygeneruj losowy kolor
        // (zeby kolor nie byl podoobny do koloru czarnego (tlo)
        // losowanie kazdej skladowej jest z zakresu (65, 256)
        private Color RandomColor()
        {
            return Color.FromArgb(
                random.Next(64, 256),
                random.Next(64, 256),
                random.Next(64, 256));
        }

        // Wyczysc plansze
        private void ClearGrid()
        {
            for (int i = 0; i < GRID_DIM; i++)
            {
                for (int j = 0; j < GRID_DIM; j++)
                {
                    grid[i, j] = new Cell();
                }
            }
        }

        // Uruchom timer symulacji po nacisnieciu przycisku
        private void GrainGrowthButton_Click(object sender, EventArgs e)
        {
            if (timer != null)
            {
                timer.Stop();
                Reset();
            }
            if (IsGridFull())
            {
                Reset();
            }
            timer = new Timer();
            timer.Interval = 100;
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        // Tick timera (czyli akcja wykonywana co okreslony czas)
        private void Timer_Tick(object sender, EventArgs e)
        {
            SimulationStep();
        }

        // Funkcja rysujaca obszaru panelu (czyli obszar planszy)
        private void GridPanel_Paint(object sender, PaintEventArgs e)
        {
            if (grid == null)
            {
                return;
            }

            for (int i = 0; i < GRID_DIM; i++)
            {
                for (int j = 0; j < GRID_DIM; j++)
                {
                    if (grid[i, j] != null)
                    {
                        e.Graphics.FillRectangle(
                            new SolidBrush(grid[i, j].color),
                            i * CELL_SIZE,
                            j * CELL_SIZE,
                            CELL_SIZE,
                            CELL_SIZE);
                    }
                }
            }
        }

        // wyznacz punkty, ktore sa na granicy ziaren
        private List<Point> PointsOnGrainBoundrary()
        {
            List<Point> points = new List<Point>();
            for (int x = 2; x < GRID_DIM - 2; x++)
            {
                for (int y = 2; y < GRID_DIM - 2; y++)
                {
                    if (IsOnGrainBoundary(x, y))
                    {
                        points.Add(new Point(x, y));
                    }
                }
            }
            return points;
        }

        // czy punkt jest na granicy ziaren
        private bool IsOnGrainBoundary(int x, int y)
        {
            HashSet<Color> colors = new HashSet<Color>();

            foreach (Point p in mooreDirections)
            {
                int nx = x + p.X;
                int ny = y + p.Y;
                if (InsideGrid(nx, ny))
                {
                    colors.Add(grid[nx, ny].color);
                }
            }
            return colors.Count > 1;
        }

        // Resetuj symulacje / plansze
        private void Reset()
        {
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }

            numberOfGrains = (int)numberOfGrainsNumericUpDown.Value;
            numberOfInclusions = (int)numberOfInclusionsNumericUpDown.Value;
            sizeOfInclusion = (int)sizeOfInclusionNumericUpDown.Value;

            ClearGrid();
            InitGrains();
            if (timeOfCreationOfInclusionsComboBox.SelectedIndex == 0)
            {
                InitInclusions();
            }        
            gridPanel.Refresh();
        }

        // akcja wykonywana po nacisnieciu przycisku Reset
        private void ResetButton_Click(object sender, EventArgs e)
        {
            Reset();
        }

        // Eksport to pliku tekstowego
        private void ExportToTxtButton_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();

            saveFileDialog.Filter = "txt files (*.txt)|*.txt";
            saveFileDialog.RestoreDirectory = true;

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                StringBuilder sb = new StringBuilder();
                for (int x = 0; x < GRID_DIM; x++)
                {
                    for (int y = 0; y < GRID_DIM; y++)
                    {
                        sb.AppendFormat(
                            "{0} {1} {2} {3} {4} {5}\n",
                            x,
                            y,
                            (int)grid[x, y].type,
                            grid[x, y].color.R,
                            grid[x, y].color.G,
                            grid[x, y].color.B);
                    }
                }
                File.WriteAllText(saveFileDialog.FileName, sb.ToString());
            }
        }

        // Import z pliku tekstowego
        private void ImportFromTxtButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "txt files (*.txt)|*.txt";
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                string[] lines = File.ReadAllLines(openFileDialog.FileName);

                int i = 0;

                for (int x = 0; x < GRID_DIM; x++)
                {
                    for (int y = 0; y < GRID_DIM; y++)
                    {
                        string line = lines[i];
                        string[] values = line.Split();
                        grid[x, y].type = (CellType)int.Parse(values[2]);
                        grid[x, y].color = Color.FromArgb(
                            int.Parse(values[3]),
                            int.Parse(values[4]),
                            int.Parse(values[5]));
                        i++;
                    }
                }
                gridPanel.Refresh();
            }
        }

        // Eksport to pliku bmp
        private void ExportToBmpButton_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();

            saveFileDialog.Filter = "bmp files (*.bmp)|*.bmp";
            saveFileDialog.RestoreDirectory = true;

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                Graphics graphics = gridPanel.CreateGraphics();
                Bitmap bitmap = new Bitmap(gridPanel.Width, gridPanel.Height);
                gridPanel.DrawToBitmap(bitmap, new Rectangle(0, 0, gridPanel.Width, gridPanel.Height));
                bitmap.Save(saveFileDialog.FileName);
            }
        }

        // Import z pliku bmp
        private void ImportFromBmpButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "bmp files (*.bmp)|*.bmp";
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                Bitmap bitmap = new Bitmap(openFileDialog.FileName);

                for (int x = 0; x < GRID_DIM; x++)
                {
                    for (int y = 0; y < GRID_DIM; y++)
                    {
                        grid[x, y].color = bitmap.GetPixel(x * CELL_SIZE, y * CELL_SIZE);
                    }
                }
                gridPanel.Refresh();
            }
        }

        private void NumberOfGrainsNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            Reset();
        }

        private void NumberOfInclusionsNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            Reset();
        }

        private void SizeOfInclusionNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            Reset();
        }

        private void ShapeOfInclusionComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            Reset();
        }

        private void TimeOfCreationOfInclusionsComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            Reset();
        }
    }
}
