using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Linq;
using System.Drawing.Imaging;

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
        private Cell[,] tempGrid = null;
        private Cell[,] recrystalizationGrid;

        private int numberOfGrains = 5;
        private int numberOfInclusions = 5;
        private int sizeOfInclusion = 2;
        private Timer timer = null;

        private Algorithm algorithm = Algorithm.GRAIN_GROWTH;

        // iteracja dla monte-carlo
        int iteration = 0;
        int maxIteration = 500;

        int recrystalizationStep = 0;
        int maxRecrystalizationStep = 20;

        // faza dla dual phase
        int phase = 1;

        Color[] palette = null;

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
            gridPanel.MouseClick += GridPanel_MouseClick;

            grid = new Cell[GRID_DIM, GRID_DIM];
            tempGrid = new Cell[GRID_DIM, GRID_DIM];
            recrystalizationGrid = new Cell[GRID_DIM, GRID_DIM];

            for (int i = 0; i < GRID_DIM; i++)
            {
                for (int j = 0; j < GRID_DIM; j++)
                {
                    grid[i, j] = new Cell();
                    tempGrid[i, j] = new Cell();
                    recrystalizationGrid[i, j] = new Cell();
                }
            }

            neighbourhoodTypeComboBox.SelectedIndex = 0;
            shapeOfInclusionComboBox.SelectedIndex = 0;
            timeOfCreationOfInclusionsComboBox.SelectedIndex = 0;
            showComboBox.SelectedIndex = 0;
            energyDistributionComboBox.SelectedIndex = 0;
            statesNumericUpDown.Value = 4;
            selectionModeComboBox.SelectedIndex = 0;
            numberOfNucleonsComboBox.SelectedIndex = 0;

            Reset();
        }

        private void FloodFill(int x, int y, Color color)
        {
            if (InsideGrid(x, y) && grid[x, y].color == color)
            {
                grid[x, y].color = Color.Red;
                FloodFill(x - 1, y - 1, color);
                FloodFill(x - 1, y, color);
                FloodFill(x - 1, y + 1, color);
                FloodFill(x, y - 1, color);
                FloodFill(x, y + 1, color);
                FloodFill(x + 1, y - 1, color);
                FloodFill(x + 1, y, color);
                FloodFill(x + 1, y + 1, color);
            }
        }

        private void GridPanel_MouseClick(object sender, MouseEventArgs e)
        {
            if (algorithm == Algorithm.DUAL_PHASE && phase == 2)
            {
                int x = e.X / CELL_SIZE;
                int y = e.Y / CELL_SIZE;

                Color color = grid[x, y].color;

                if (selectionModeComboBox.SelectedIndex == 0)
                {
                    FloodFill(x, y, color);
                }
                else
                {
                    for (int i = 0; i < GRID_DIM; i++)
                    {
                        for (int j = 0; j < GRID_DIM; j++)
                        {
                            if (grid[i, j].color == color)
                            {
                                grid[i, j].color = Color.Red;
                            }
                        }
                    }
                }
                Refresh();
            }
        }

        // Poczatkowa inicjalizacja ziaren
        public void InitGrains()
        {
            int numberOfGrainsAdded = 0;

            while (numberOfGrainsAdded < numberOfGrains)
            {
                int x = random.Next(GRID_DIM);
                int y = random.Next(GRID_DIM);

                if (grid[x, y].type != CellType.EMPTY || grid[x, y].color == Color.Red)
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
        private void GrainGrowthSimulationStep()
        {
            // dopoki wszystkie pola nie za wypelnione
            if (!IsGridFull())
            {
                // wykonuj rozrost ziaren
                GrowGrains();
                if (iteration++ % 3 == 0)
                {
                    gridPanel.Refresh();
                }
            }
            else // jesli wszystkie pola sa wypelnione
            {
                // jesli wtracenia maja sie tworzyc na koncu symulacji 
                // to je utworz
                if (inclusionsCheckBox.Checked && 
                    timeOfCreationOfInclusionsComboBox.SelectedIndex == 1)
                {
                    InitInclusions(true);
                }
                gridPanel.Refresh();
                // zakoncz rozrost ziaren
                StopTimer();
            }
            
        }

        // krok symulacji monte carlo
        private void MonteCarloSimulationStep()
        {
            maxIteration = (int)maxIterationNumericUpDown.Value;
            if (iteration < maxIteration)
            {
                MonteCarlo();
                iteration++;
                Text = "Monte carlo: " + iteration;
            }
            else
            {
                StopTimer();
            }
        }

        private void DualPhaseSimulationStep()
        {
            if (phase == 1) // faza pierwsza
            {
                if (CA_MC_radioButton.Checked)
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
                        if (inclusionsCheckBox.Checked &&
                            timeOfCreationOfInclusionsComboBox.SelectedIndex == 1)
                        {
                            InitInclusions(true);
                        }
                        phase = 2;
                        StopTimer();
                        dualPhaseButton.Text = "Dual Phase - next phase";
                        MessageBox.Show("Select grains for next phase");
                    }
                }
                else
                {
                    maxIteration = (int)maxIterationNumericUpDown.Value;
                    if (iteration < maxIteration)
                    {
                        MonteCarlo();
                        iteration++;
                        Text = "Monte carlo: " + iteration;
                    }
                    else
                    {
                        phase = 2;
                        StopTimer();
                        dualPhaseButton.Text = "Dual Phase - next phase";
                        MessageBox.Show("Select grains for next phase");
                    }
                }
            }
            else // faza druga
            {
                if (CA_MC_radioButton.Checked)
                {
                    maxIteration = (int)maxIterationNumericUpDown.Value;
                    if (iteration < maxIteration)
                    {
                        MonteCarlo();
                        iteration++;
                        Text = "Monte carlo: " + iteration;
                    }
                    else
                    {
                        StopTimer();
                        phase = 0;
                        dualPhaseButton.Text = "Dual Phase";
                    }
                }
                else
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
                        if (inclusionsCheckBox.Checked &&
                            timeOfCreationOfInclusionsComboBox.SelectedIndex == 1)
                        {
                            InitInclusions(true);
                        }
                        phase = 0;
                        StopTimer();
                        dualPhaseButton.Text = "Dual Phase";
                    }
                }
            }

            gridPanel.Refresh();
        }

        private void StaticRecrystalizationSimulationStep()
        {
            if (recrystalizationStep < maxRecrystalizationStep)
            {
                StaticRecrystalization();
                recrystalizationStep++;
                Text = "Static recrystalization: " + recrystalizationStep;
            }
            else
            {
                StopTimer();
            }
        }

        private int kroneckerDelta(Cell a, Cell b)
        {
            return a.color == b.color ? 1 : 0;
        }

        // Algorytm MonteCarlo
        private void MonteCarlo()
        {
            int[,] direction = {
                {-1, 0}, {1, 0}, {0, -1}, {0, 1},
                {-1, -1}, {-1, 1}, {1, -1}, {1, 1}};

            for (int i = 0; i < GRID_DIM; i++)
            {
                for (int j = 0; j < GRID_DIM; j++)
                {
                    tempGrid[i, j].color = grid[i, j].color;
                }
            }

          List<Point> points = PointsOnGrainBoundrary();
            while (points.Count > 0)
            {
                // Step 1: Random selection of element with specificially orientation
                int randomIndex = random.Next(points.Count);
                Point p = points[randomIndex];
                points.RemoveAt(randomIndex);

                int x1 = p.X;
                int y1 = p.Y;

                int energyBefore = 0;

                if (grid[x1, y1].type != CellType.GRAIN ||
                    grid[x1, y1].color == Color.Red) continue;

                for (int i = 0; i < 8; i++)
                {
                    int x2 = x1 + direction[i, 0];
                    int y2 = y1 + direction[i, 1];

                    if (!InsideGrid(x2, y2) ||
                        grid[x2, y2].type != CellType.GRAIN ||
                        grid[x2, y2].color == Color.Red) continue;

                    // Step 2: Calculate the energy of lattice site with surrounding concerned
                    // element Qi
                    energyBefore += (1 - kroneckerDelta(grid[x1, y2], grid[x2, y2]));
                }

                // Step 3: The investigated cell changes state to one of the available states / orientation.
                // The state / orientation is randomly generated from OMEGA available states / orientations.
                Cell cellAfterStateChange = new Cell(RandomColorFromPalette(), CellType.GRAIN);

                int energyAfter = 0;

                for (int i = 0; i < 8; i++)
                {
                    int x2 = x1 + direction[i, 0];
                    int y2 = y1 + direction[i, 1];

                    if (!InsideGrid(x2, y2) ||
                        grid[x2, y2].type != CellType.GRAIN ||
                        grid[x2, y2].color == Color.Red) continue;

                    energyAfter += (1 - kroneckerDelta(cellAfterStateChange, grid[x2, y2]));
                }

                // Step 4: Calculate the change in energy Qi caused by orientation changes

                int energyDelta = energyAfter - energyBefore;

                // Step 5: The orientation change is accepted
                if (energyDelta <= 0)
                {
                    tempGrid[x1, y1].color = cellAfterStateChange.color;
                }
            }

            for (int i = 0; i < GRID_DIM; i++)
            {
                for (int j = 0; j < GRID_DIM; j++)
                {
                    grid[i, j].color = tempGrid[i, j].color;
                }
            }

            Refresh();
        }

        private void StaticRecrystalization()
        {
            int numberOfNucleons = 100;
            
            if (numberOfNucleonsComboBox.SelectedIndex == 1)
            {
                numberOfNucleons = (recrystalizationStep + 1) * 10;
            }

            List<Point> possibleNucleationPoints = new List<Point>();
            for (int i = 0; i < GRID_DIM; i++)
            {
                for (int j = 0; j < GRID_DIM; j++)
                {
                    if (grid[i, j].energy > 0)
                    {
                        possibleNucleationPoints.Add(new Point(i, j));
                    }
                }
            }

            if (numberOfNucleons > possibleNucleationPoints.Count)
            {
                numberOfNucleons = possibleNucleationPoints.Count;
            }

            possibleNucleationPoints.OrderBy(p => random.Next()).ToList();
            possibleNucleationPoints.Sort((a, b) => 
            grid[b.X, b.Y].energy.CompareTo(grid[a.X, a.Y].energy));

            if (energyDistributionComboBox.SelectedIndex == 0)
            {
                for (int i = 0; i < numberOfNucleons; i++)
                {
                    Point p = possibleNucleationPoints[i];

                    if (recrystalizationGrid[p.X, p.Y].type == CellType.EMPTY)
                    {
                        recrystalizationGrid[p.X, p.Y] = new Cell(
                            Color.FromArgb(random.Next(256), 100, 100),
                            CellType.GRAIN);
                    }
                }
            }
            else
            {
                for (int i = 0; i < numberOfNucleons; i++)
                {
                    Point p = possibleNucleationPoints[random.Next(possibleNucleationPoints.Count)];
                    possibleNucleationPoints.Remove(p);

                    if (recrystalizationGrid[p.X, p.Y].type == CellType.EMPTY)
                    {
                        recrystalizationGrid[p.X, p.Y] = new Cell(
                            Color.FromArgb(random.Next(256), 100, 100),
                            CellType.GRAIN);
                    }
                }
            }


            RecrystalizationGrowth();
            Refresh();
        }

        private void RecrystalizationGrowth()
        {
            Cell[,] tempRecrystalizationGrid = CopyGrid(recrystalizationGrid);

            int[,] direction = {
                {-1, 0}, {1, 0}, {0, -1}, {0, 1},
                {-1, -1}, {-1, 1}, {1, -1}, {1, 1}};

            List<Point> points = new List<Point>();

            for (int x = 0; x < GRID_DIM; x++)
            {
                for (int y = 0; y < GRID_DIM; y++)
                {
                    if (recrystalizationGrid[x, y].type == CellType.EMPTY)
                    {
                        points.Add(new Point(x, y));
                    }
                }
            }

            // Are all sites checked?
            while (points.Count > 0)
            {
                // Select site randomly
                int pointIndex = random.Next(points.Count);
                Point randomPoint = points[pointIndex];
                points.RemoveAt(pointIndex);

                int x1 = randomPoint.X;
                int y1 = randomPoint.Y;

                List<Point> neighbourPoints = new List<Point>();

                for (int k = 0; k < 8; k++)
                {
                    int nx = x1 + direction[k, 0];
                    int ny = y1 + direction[k, 1];
                    if (InsideGrid(nx, ny))
                    {
                        neighbourPoints.Add(new Point(nx, ny));
                    }
                }

                // Select site's neighbour randomly
                Point randomNeighbour = neighbourPoints[random.Next(neighbourPoints.Count)];

                // Is neighbour site recrystalized
                if (recrystalizationGrid[randomNeighbour.X, randomNeighbour.Y].type != CellType.GRAIN)
                {
                    continue;
                }

                int energyBefore = 0;


                // Compute energry before reorientation
                for (int i = 0; i < 8; i++)
                {
                    int x2 = x1 + direction[i, 0];
                    int y2 = y1 + direction[i, 1];

                    if (!InsideGrid(x2, y2) ||
                        recrystalizationGrid[x2, y2].type != CellType.GRAIN) continue;

                    energyBefore += (1 - kroneckerDelta(
                        recrystalizationGrid[x1, y2],
                        recrystalizationGrid[x2, y2]));
                }

                Cell cellAfterStateChange = new Cell(
                    recrystalizationGrid[randomNeighbour.X, randomNeighbour.Y].color,
                    CellType.GRAIN);

                int energyAfter = 0;

                // Compute energry after reorientation
                for (int i = 0; i < 8; i++)
                {
                    int x2 = x1 + direction[i, 0];
                    int y2 = y1 + direction[i, 1];

                    if (!InsideGrid(x2, y2) ||
                        recrystalizationGrid[x2, y2].type != CellType.GRAIN) continue;

                    energyAfter += (1 - kroneckerDelta(
                        cellAfterStateChange,
                        recrystalizationGrid[x2, y2]));
                }


                // Is the energy not higher
                int energyDelta = energyAfter - energyBefore;
                if (energyDelta <= 0)
                {
                    tempRecrystalizationGrid[x1, y1].type = cellAfterStateChange.type;
                    tempRecrystalizationGrid[x1, y1].color = cellAfterStateChange.color;
                }
            }

            recrystalizationGrid = CopyGrid(tempRecrystalizationGrid);
            Refresh();
        }

        private void CalculateEnergy()
        {
            for (int i = 0; i < GRID_DIM; i++)
            {
                for (int j = 0; j < GRID_DIM; j++)
                {
                    grid[i, j].energy = CalculateEnergy(i, j);
                }
            }
        }

        private int CalculateEnergy(int x, int y)
        {
            int[,] d =
            {
                {-1, 0}, {1, 0}, {0, -1}, {0, 1},
                {-1, -1}, {-1, 1}, {1, -1}, {1, 1}
            };

            if (energyDistributionComboBox.SelectedIndex == 1)
            {
                return 3;
            }

            int energy = 0;

            // energy was taken by recrystalization
            if (recrystalizationGrid[x, y].color != Color.Black)
            {
                return 0;
            }

            for (int k = 0; k < 8; k++)
            {
                int dx = d[k, 0];
                int dy = d[k, 1];

                if (InsideGrid(x + dx, y + dy))
                {
                    if (grid[x, y].color != Color.Black &&
                        grid[x, y].type == CellType.GRAIN &&
                        grid[x + dx, y + dy].type == CellType.GRAIN &&
                        grid[x, y].color != grid[x + dx, y + dy].color)
                    {
                        energy++;
                    }

                }
            }

            return energy;
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

                    if (color == Color.Black || color == Color.Red)
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

        // zatrzymaj timer
        void StopTimer()
        {
            timer.Stop();
            timer = null;
        }

        // przekopiuj plansze
        Cell[,] CopyGrid(Cell[,] a)
        {
            Cell[,] b = new Cell[GRID_DIM, GRID_DIM];

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

        private Color RandomColorFromPalette() // random state
        {
            return palette[random.Next(palette.Length)];
        }

        // Wyczysc plansze
        private void ClearGrid()
        {
            for (int i = 0; i < GRID_DIM; i++)
            {
                for (int j = 0; j < GRID_DIM; j++)
                {
                    grid[i, j] = new Cell();
                    tempGrid[i, j] = new Cell();
                    recrystalizationGrid[i, j] = new Cell();
                }
            }
        }

        private void RunAlgorithm()
        {
            if (timer != null)
            {
                timer.Stop();
                Reset();
            }
            if (algorithm == Algorithm.GRAIN_GROWTH && IsGridFull())
            {
                Reset();
            }
            timer = new Timer();
            timer.Interval = 200;
            timer.Tick += Timer_Tick;
            timer.Start();
        }

        private void GrainGrowthButton_Click(object sender, EventArgs e)
        {
            algorithm = Algorithm.GRAIN_GROWTH;
            RunAlgorithm();
        }

        private void MonteCarloButton_Click(object sender, EventArgs e)
        {
            algorithm = Algorithm.MONTE_CARLO;
            Reset();

            for (int i = 0; i < GRID_DIM; i++)
            {
                for (int j = 0; j < GRID_DIM; j++)
                {
                    grid[i, j] = new Cell(RandomColorFromPalette(), CellType.GRAIN);
                    tempGrid[i, j] = new Cell();
                }
            }
            Refresh();
            RunAlgorithm();
        }

        private void InitMonteCarlo()
        {
            for (int i = 0; i < GRID_DIM; i++)
            {
                for (int j = 0; j < GRID_DIM; j++)
                {
                    grid[i, j] = new Cell(RandomColorFromPalette(), CellType.GRAIN);
                    tempGrid[i, j] = new Cell();
                }
            }
        }

        private void DualPhaseButton_Click(object sender, EventArgs e)
        {
            if (phase == 0) phase++;
            algorithm = Algorithm.DUAL_PHASE;
            if (phase == 1)
            {
                if (MC_CA_radioButton.Checked)
                {
                    InitMonteCarlo();
                }               
            }

            else if (phase == 2)
            {
                if (CA_MC_radioButton.Checked)
                {
                    for (int i = 0; i < GRID_DIM; i++)
                    {
                        for (int j = 0; j < GRID_DIM; j++)
                        {
                            if (grid[i, j].type == CellType.GRAIN && 
                                grid[i, j].color != Color.Red)
                            {
                                grid[i, j] = new Cell(RandomColorFromPalette(), CellType.GRAIN);
                                tempGrid[i, j] = new Cell();
                            }
                        }
                    }
                }
                else
                {
                    for (int i = 0; i < GRID_DIM; i++)
                    {
                        for (int j = 0; j < GRID_DIM; j++)
                        {
                            if (grid[i, j].type == CellType.GRAIN &&
                                grid[i, j].color != Color.Red)
                            {
                                grid[i, j] = new Cell();
                                tempGrid[i, j] = new Cell();
                            }
                        }
                    }

                    InitGrains();
                }
            }

            RunAlgorithm();
        }

        private void StaticRecrystalizationButton_Click(object sender, EventArgs e)
        {
            if (!IsGridFull())
            {
                MessageBox.Show("There is no structure!");
                return;
            }

            algorithm = Algorithm.STATIC_RECRYSTALIZATION;
            RunAlgorithm();
        }

        // Tick timera (czyli akcja wykonywana co okreslony czas)
        private void Timer_Tick(object sender, EventArgs e)
        {
            if (algorithm == Algorithm.GRAIN_GROWTH) GrainGrowthSimulationStep();
            else if (algorithm == Algorithm.MONTE_CARLO) MonteCarloSimulationStep();
            else if (algorithm == Algorithm.DUAL_PHASE) DualPhaseSimulationStep();
            else if (algorithm == Algorithm.STATIC_RECRYSTALIZATION) StaticRecrystalizationSimulationStep();
            CalculateEnergy();
        }

        // Funkcja rysujaca obszaru panelu (czyli obszar planszy)
        private void GridPanel_Paint(object sender, PaintEventArgs e)
        {
            if (grid == null)
            {
                return;
            }

            Bitmap bitmap = new Bitmap(
                GRID_DIM * CELL_SIZE,
                GRID_DIM * CELL_SIZE,
                PixelFormat.Format32bppArgb);

            BitmapData bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadWrite,
                bitmap.PixelFormat);

            int PixelSize = 4;

            unsafe
            {
                for (int y = 0; y < bitmapData.Height; y += CELL_SIZE)
                {
                    byte* r1 = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);

                    for (int x = 0; x < bitmapData.Width; x += CELL_SIZE)
                    {
                        int px = x / CELL_SIZE;
                        int py = y / CELL_SIZE;

                        Cell cell = grid[px, py];

                        if (recrystalizationGrid[px, py].color != Color.Black)
                        {
                            cell = recrystalizationGrid[px, py];
                        }

                        Color color = cell.color;

                        if (showComboBox.SelectedIndex == 1)
                        {
                            color = energyColor(cell.energy);
                        }

                        r1[x * PixelSize] = color.B;
                        r1[x * PixelSize + 1] = color.G;
                        r1[x * PixelSize + 2] = color.R;
                        r1[x * PixelSize + 3] = 255;

                        r1[(x + 1) * PixelSize] = color.B;
                        r1[(x + 1) * PixelSize + 1] = color.G;
                        r1[(x + 1) * PixelSize + 2] = color.R;
                        r1[(x + 1) * PixelSize + 3] = 255;
                    }

                    byte* r2 = (byte*)bitmapData.Scan0 + ((y + 1) * bitmapData.Stride);

                    for (int x = 0; x < bitmapData.Width; x += CELL_SIZE)
                    {
                        int px = x / CELL_SIZE;
                        int py = y / CELL_SIZE;

                        Cell cell = grid[px, py];

                        if (recrystalizationGrid[px, py].color != Color.Black)
                        {
                            cell = recrystalizationGrid[px, py];
                        }

                        Color color = cell.color;

                        if (showComboBox.SelectedIndex == 1)
                        {
                            color = energyColor(cell.energy);
                        }

                        r2[x * PixelSize] = color.B;
                        r2[x * PixelSize + 1] = color.G;
                        r2[x * PixelSize + 2] = color.R;
                        r2[x * PixelSize + 3] = 255;

                        r2[(x + 1) * PixelSize] = color.B;
                        r2[(x + 1) * PixelSize + 1] = color.G;
                        r2[(x + 1) * PixelSize + 2] = color.R;
                        r2[(x + 1) * PixelSize + 3] = 255;
                    }
                }
            }

            bitmap.UnlockBits(bitmapData);
            e.Graphics.DrawImage(bitmap, 0, 0);
        }

        private Color energyColor(int level)
        {
            if (level <= 1) return Color.Green;
            else if (level <= 3) return Color.Yellow;
            else if (level <= 5) return Color.Orange;
            else return Color.Red;
        }

        // wyznacz punkty, ktore sa na granicy ziaren
        private List<Point> PointsOnGrainBoundrary()
        {
            List<Point> points = new List<Point>();
            for (int x = 0; x < GRID_DIM; x++)
            {
                for (int y = 0; y < GRID_DIM; y++)
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

            colors.Add(grid[x, y].color);
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
            iteration = 0;
            recrystalizationStep = 0;
            phase = 0;
            Text = "";
            dualPhaseButton.Text = "Dual Phase";

            ClearGrid();
            if (algorithm == Algorithm.GRAIN_GROWTH ||
                (algorithm == Algorithm.DUAL_PHASE && CA_MC_radioButton.Checked))
            {
                InitGrains();
                if (inclusionsCheckBox.Checked && timeOfCreationOfInclusionsComboBox.SelectedIndex == 0)
                {
                    InitInclusions();
                }
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

        private void statesNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            int n = (int)statesNumericUpDown.Value;
            palette = new Color[n];
            for (int i = 0; i < n; i++)
            {
                palette[i] = Color.FromArgb(
                    0,
                    64 + random.Next(0, 192),
                    64 + random.Next(0, 192));
            }
        }

        private void showComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            Refresh();
        }

        private void energyDistributionComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            CalculateEnergy();
            Refresh();
        }

        private void inclusionsCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            Reset();
        }

        private void recrystalizationStepsNumericUpDown_ValueChanged(object sender, EventArgs e)
        {
            maxRecrystalizationStep = (int)recrystalizationStepsNumericUpDown.Value;
        }
    }
}