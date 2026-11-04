using System.Collections.Generic;
using Game.UI.Framework;

namespace Game.Economy
{
    /// <summary>串行弹出优惠商品面板，避免多个同时叠在一起；仅弹出当前玩法作用域内的商品。</summary>
    public static class DiscountOfferPopupQueue
    {
        static readonly Queue<int> Pending = new Queue<int>();
        static bool _draining;
        static int _currentOfferId;

        public static void Enqueue(int offerId)
        {
            if (offerId <= 0 || !DiscountOfferSaveBridge.IsActive || !DiscountOfferSaveBridge.MatchesScope(offerId))
                return;

            if (Contains(offerId))
                return;

            Pending.Enqueue(offerId);
            TryDrain();
        }

        public static void NotifyPanelRegistered()
        {
            TryDrain();
        }

        public static void NotifyPanelClosed()
        {
            _draining = false;
            _currentOfferId = 0;
            TryDrain();
        }

        public static void NotifyOpenFailed(int offerId)
        {
            if (_currentOfferId == offerId)
            {
                _draining = false;
                _currentOfferId = 0;
            }

            TryDrain();
        }

        public static void OnActiveScopeChanged()
        {
            var rebuilt = new Queue<int>();
            while (Pending.Count > 0)
            {
                var offerId = Pending.Dequeue();
                if (DiscountOfferSaveBridge.MatchesScope(offerId))
                    rebuilt.Enqueue(offerId);
            }

            while (rebuilt.Count > 0)
                Pending.Enqueue(rebuilt.Dequeue());

            if (_draining && _currentOfferId > 0 && !DiscountOfferSaveBridge.MatchesScope(_currentOfferId))
            {
                _draining = false;
                _currentOfferId = 0;
            }
        }

        static bool Contains(int offerId)
        {
            if (_draining && _currentOfferId == offerId)
                return true;

            foreach (var id in Pending)
            {
                if (id == offerId)
                    return true;
            }

            return false;
        }

        static void TryDrain()
        {
            if (_draining || Pending.Count == 0 || !DiscountOfferSaveBridge.IsActive)
                return;

            if (!UiCoordinator.TryGet(UiPanelId.DiscountOffer, out _))
                return;

            while (Pending.Count > 0)
            {
                var next = Pending.Dequeue();
                if (!DiscountOfferSaveBridge.MatchesScope(next))
                    continue;

                _draining = true;
                _currentOfferId = next;
                UiRouter.OpenDiscountOffer(_currentOfferId);
                return;
            }
        }
    }
}
