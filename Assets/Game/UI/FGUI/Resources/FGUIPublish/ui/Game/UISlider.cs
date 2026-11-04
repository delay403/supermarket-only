/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Game
{
    public partial class UISlider : GComponent
    {
        public GLoader m_Background;
        public GLoader m_Fill;
        public GLoader m_Icon;
        public GTextField m_Label;
        public const string URL = "ui://6laopsucicg1nv";

        public static UISlider CreateInstance()
        {
            return (UISlider)UIPackage.CreateObject("Game", "Slider");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_Background = (GLoader)GetChildAt(0);
            m_Fill = (GLoader)GetChildAt(1);
            m_Icon = (GLoader)GetChildAt(2);
            m_Label = (GTextField)GetChildAt(3);
        }
    }
}