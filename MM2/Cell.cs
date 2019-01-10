using System.Drawing;

namespace CA
{
    public class Cell
    {
        public Color color;
        public CellType type;
        public int energy;

        public Cell()
        {
            color = Color.Black;
            type = CellType.EMPTY;
            energy = 0;
        }

        public Cell(Color color, CellType type)
        {
            this.color = color;
            this.type = type;
            this.energy = 0;
        }
    }
}
