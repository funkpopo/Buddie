using System.Windows.Media;

namespace Buddie
{
    public class CardData
    {
        public string FrontText { get; set; } = "";
        public string FrontSubText { get; set; } = "";
        public string BackText { get; set; } = "";
        public string BackSubText { get; set; } = "";
        public Brush FrontBackground { get; set; } = Brushes.LightBlue;
        public Brush BackBackground { get; set; } = Brushes.LightCoral;

        // 关联的API配置
        public OpenApiConfiguration? ApiConfiguration { get; set; }
    }
}

