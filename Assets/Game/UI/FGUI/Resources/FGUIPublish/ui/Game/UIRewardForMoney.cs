/** This is an automatically generated class by FairyGUI. Please do not modify it. **/

using FairyGUI;
using FairyGUI.Utils;

namespace Game
{
    public partial class UIRewardForMoney : GButton
    {
        public GTextField m_LabelTime;
        public Transition m_t1;
        public const string URL = "ui://6laopsucicg1qy";

        public static UIRewardForMoney CreateInstance()
        {
            return (UIRewardForMoney)UIPackage.CreateObject("Game", "AdForMoney");
        }

        public override void ConstructFromXML(XML xml)
        {
            base.ConstructFromXML(xml);

            m_LabelTime = (GTextField)GetChildAt(2);
            m_t1 = GetTransitionAt(0);
        }
    }
}