/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Game
{
    public partial class UICtrpClimb : GButton
    {
        public GLoader m_Fill;
        public GLoader m_Icon;
        public GTextField m_Label;
        public GLoader m_gouk;
        public GLoader m_gou;
        public const string URL = "ui://6laopsucicg1nt";

        public static UICtrpClimb CreateInstance()
        {
            return (UICtrpClimb)UIPackage.CreateObject("Game", "CtrpClimb");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_Fill = (GLoader)GetChildAt(0);
            m_Icon = (GLoader)GetChildAt(1);
            m_Label = (GTextField)GetChildAt(2);
            m_gouk = (GLoader)GetChildAt(3);
            m_gou = (GLoader)GetChildAt(4);
        }
    }
}