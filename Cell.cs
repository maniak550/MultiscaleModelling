using System.Drawing;

namespace CA
{
    public class Cell
    {
        public Color color;
        public CellType type;

        public Cell()
        {
            color = Color.Black;
            type = CellType.EMPTY;
        }

        public Cell(Color color, CellType type)
        {
            this.color = color;
            this.type = type;
        }
    }
}
