using UnityEngine;

namespace Game.SupermarketOrganize
{
    public struct SupermarketAimState
    {
        public bool hasAimTarget;
        public bool canPickUp;
        public bool canPlace;
        public bool showShelfFeedback;
        public bool shelfIsCorrect;
        public bool showShelfExtraTip;
        public bool useHintTextColor;
        public Color hintTextColor;
        public string hintText;
        public string pickUpLabel;
        public string placeLabel;
    }
}
