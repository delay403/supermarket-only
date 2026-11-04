/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Home
{
    public partial class UILabelCoin : GLabel
    {
        public GImage m_add;
        public const string URL = "ui://l851vxyyk8pln5";

        public static UILabelCoin CreateInstance()
        {
            return (UILabelCoin)UIPackage.CreateObject("Home", "LabelCoin");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_add = (GImage)GetChildAt(2);
        }
    }
}