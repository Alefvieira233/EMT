using Autodesk.Revit.DB;

namespace FerramentaEMT.Forms
{
    public class SymbolItem
    {
        public FamilySymbol Symbol { get; private set; }
        public string Text { get; private set; }

        public SymbolItem(FamilySymbol s)
        {
            Symbol = s;
            Text = s.FamilyName + " : " + s.Name;
        }

        public override string ToString()
        {
            return Text;
        }
    }

    public class ZJustificationItem
    {
        public int Value { get; private set; }
        public string Text { get; private set; }

        public ZJustificationItem(int value, string text)
        {
            Value = value;
            Text = text;
        }

        public override string ToString()
        {
            return Text;
        }
    }

    public class LevelItem
    {
        public Level Level { get; private set; }
        public string Text { get; private set; }

        public LevelItem(Level level)
        {
            Level = level;
            Text = level != null ? level.Name : string.Empty;
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
