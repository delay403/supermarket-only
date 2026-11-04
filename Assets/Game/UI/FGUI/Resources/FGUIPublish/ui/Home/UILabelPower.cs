/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Home
{
    public partial class UILabelPower : GLabel
    {
        public GImage m_add;
        public const string URL = "ui://l851vxyyk8plna";

        public static UILabelPower CreateInstance()
        {
            return (UILabelPower)UIPackage.CreateObject("Home", "LabelPower");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_add = (GImage)GetChildAt(2);
        }
    }
}