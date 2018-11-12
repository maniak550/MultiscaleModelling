﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

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

            neighbourhoodTypeComboBox.SelectedIndex = 0;
            shapeOfInclusionComboBox.SelectedIndex = 0;
            timeOfCreationOfInclusionsComboBox.SelectedIndex = 0;

            grid = new Cell[GRID_DIM, GRID_DIM];
            for (int i = 0; i < GRID_DIM; i++)
            {
                for (int j = 0; j < GRID_DIM; j++)
                {
                    grid[i, j] = new Cell();
                }
            }

            Reset();
        }

        // Poczatkowa inicjalizacja ziaren
        public void InitGrains()
        {
            for (int i = 0; i < numberOfGrains; i++)
            {
                int x = random.Next(GRID_DIM);
                int y = random.Next(GRID_DIM);

                Color color = RandomColor();

                if (InsideGrid(x, y))
                {
                    grid[x, y] = new Cell(color, CellType.GRAIN);
                }
            }
        }

        // Inicjalizacja wtracen
        // onGrainBoundrary - czy wtracenia maja sie tworzyc na granicach ziaren
        public void InitInclusions(bool onGrainBoundrary = false)
        {
            for (int i = 0; i < numberOfInclusions; i++)
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
                    x = random.Next(GRID_DIM);
                    y = random.Next(GRID_DIM);
                }
                if (shapeOfInclusionComboBox.SelectedIndex == 0)
                {
                    InitSquareInclusion(x, y);
                }
                else if (shapeOfInclusionComboBox.SelectedIndex == 1)
                {
                    InitCircularInclusion(x, y);
                }
            }
        }

        // Utworz wtracenie o kwadratowym ksztalcie
        private void InitSquareInclusion(int x, int y)
        {
            Color color = RandomColor();

            for (int dx = -sizeOfInclusion; dx <= sizeOfInclusion; dx++)
            {
                for (int dy = -sizeOfInclusion; dy <= sizeOfInclusion; dy++)
                {
                    int nx = x + dx;
                    int ny = y + dy;

                    if (InsideGrid(nx, ny))
                    {
                        grid[nx, ny] = new Cell(color, CellType.INCLUSION);
                    }
                }
            }
        }

        // Utrzow wtracenie o okraglym ksztalcie
        private void InitCircularInclusion(int x, int y)
        {
            Color color = RandomColor();

            int r = sizeOfInclusion;

            for (int dx = -sizeOfInclusion; dx <= sizeOfInclusion; dx++)
            {
                for (int dy = -sizeOfInclusion; dy <= sizeOfInclusion; dy++)
                {
                    int nx = x + dx;
                    int ny = y + dy;

                    if (InsideGrid(nx, ny) && dx*dx + dy*dy <= r*r)
                    {
                        grid[nx, ny] = new Cell(color, CellType.INCLUSION);
                    }
                }
            }
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

        // wzrost ziaren
        void GrowGrains()
        {
            Cell[,] gridCopy = CopyGrid(grid);

            for (int x = 0; x < GRID_DIM; x++)
            {
                for (int y = 0; y < GRID_DIM; y++)
                {
                    if (CanGrow(x, y))
                    {
                        foreach (Point p in Neighbours(x, y))
                        {
                            gridCopy[p.X, p.Y].color = gridCopy[x, y].color;
                            gridCopy[p.X, p.Y].type = gridCopy[x, y].type;
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
                if (Available(nx, ny))
                {
                    neighbours.Add(new Point(nx, ny));
                }
            }
            return neighbours;
        }

        // czy pole jest dostepne (czyli czy jest puste)
        private bool Available(int x, int y)
        {
            return InsideGrid(x, y) && grid[x, y].type == CellType.EMPTY;
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
        private void resetButton_Click(object sender, EventArgs e)
        {
            Reset();
        }

    
        }
    }
}