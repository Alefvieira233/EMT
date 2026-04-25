namespace FerramentaEMT.Models.PF
{
    public sealed class PfRebarShapeOption
    {
        public long ElementIdValue { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool IsAutomatic { get; set; }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(DisplayName) ? Name : DisplayName;
        }
    }
}
