/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Game
{
    public partial class UIChapion : GComponent
    {
        public GLoader m_BG;
        public GTextField m_Label;
        public const string URL = "ui://6laopsucicg1qi";

        public static UIChapion CreateInstance()
        {
            return (UIChapion)UIPackage.CreateObject("Game", "Chapion");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_BG = (GLoader)GetChildAt(0);
            m_Label = (GTextField)GetChildAt(1);
        }
    }
}