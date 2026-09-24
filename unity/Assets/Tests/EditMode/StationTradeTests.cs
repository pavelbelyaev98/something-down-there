using NUnit.Framework;
using System.Linq;

namespace SomethingDownThere.Tests
{
    public sealed class StationTradeTests
    {
        private SessionInventory bag;
        private SessionWallet wallet;
        private ShovelState shovel;
        private StationTrade trade;

        [SetUp]
        public void SetUp()
        {
            bag = new SessionInventory();
            wallet = new SessionWallet();
            shovel = new ShovelState(EquipmentProgression.ToolProfiles());
            trade = new StationTrade(bag, wallet, shovel);
            bag.TryAdd(new InventoryItem("first", "Coin", 5));
            bag.TryAdd(new InventoryItem("second", "Coin", 17));
        }

        [Test]
        public void SellOneUsesSelectedIdentityAndEveryOfferCommitsAtMostOnce()
        {
            var first = bag.Items[0];
            var one = trade.OfferSale("second");
            var all = trade.OfferSale();
            Assert.That(wallet.Balance, Is.Zero);
            Assert.That(bag.Count, Is.EqualTo(2));
            Assert.That(trade.TrySell(one), Is.True);
            Assert.That(bag.Items, Is.EqualTo(new[] { first }));
            Assert.That(wallet.Balance, Is.EqualTo(17));
            Assert.That(trade.TrySell(one), Is.False);
            Assert.That(trade.TrySell(all), Is.False, "An earlier Sell All cannot silently change its contents.");
            Assert.That(trade.TrySell(trade.OfferSale()), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(22));
            Assert.That(bag.Count, Is.Zero);
            Assert.That(trade.Check(trade.OfferSale()), Is.EqualTo(TradeResult.Empty));
        }

        [TestCase("replacement")]
        [TestCase("bag restored")]
        [TestCase("wallet restored")]
        public void MutationsInvalidateDisplayedSalesEvenWhenTheSameValuesReturn(string change)
        {
            var quote = trade.OfferSale();
            if (change == "wallet restored") { wallet.TryCredit(1); wallet.TrySpend(1); }
            else
            {
                bag.TryRemove("first", out var removed);
                bag.TryAdd(change == "replacement" ? new InventoryItem("first", "Coin", 99) : removed);
            }
            int count = bag.Count;
            decimal balance = wallet.Balance;
            Assert.That(trade.Check(quote), Is.EqualTo(TradeResult.Changed));
            Assert.That(trade.TrySell(quote), Is.False);
            Assert.That(bag.Count, Is.EqualTo(count));
            Assert.That(wallet.Balance, Is.EqualTo(balance));
        }

        [Test]
        public void CreditOverflowRejectsTheEntireSaleAndMissingOrForeignOffersDoNothing()
        {
            wallet.TryCredit(int.MaxValue - 10);
            var quote = trade.OfferSale();
            Assert.That(trade.Check(quote), Is.EqualTo(TradeResult.CreditLimit));
            Assert.That(trade.TrySell(quote), Is.False);
            Assert.That(bag.Count, Is.EqualTo(2));
            Assert.That(wallet.Balance, Is.EqualTo(int.MaxValue - 10));
            Assert.That(trade.TrySell(trade.OfferSale("missing")), Is.False);
            var other = new StationTrade(bag, wallet, shovel);
            Assert.That(other.TrySell(trade.OfferSale("first")), Is.False);
            Assert.That(other.TryUpgrade(trade.OfferUpgrade()), Is.False);
        }

        [Test]
        public void UpgradeCostsAreSequentialAffordableAndQuotedWithoutMutation()
        {
            var first = trade.OfferUpgrade();
            Assert.That(first.Cost, Is.EqualTo(10));
            Assert.That(first.NextLevel, Is.EqualTo(2));
            Assert.That(trade.Check(first), Is.EqualTo(TradeResult.Unaffordable));
            Assert.That(trade.TryUpgrade(first), Is.False);
            Assert.That(shovel.Level, Is.EqualTo(1));
            wallet.TryCredit(Enumerable.Range(1, EquipmentProgression.LevelCount - 1).Sum(EquipmentProgression.Price));
            Assert.That(trade.TryUpgrade(first), Is.False, "A changed balance needs a fresh displayed offer.");
            int[] prices = Enumerable.Range(1, EquipmentProgression.LevelCount - 1).Select(EquipmentProgression.Price).ToArray();
            for (int i = 0; i < prices.Length; i++)
            {
                decimal before = wallet.Balance;
                var quote = trade.OfferUpgrade();
                Assert.That(quote.Cost, Is.EqualTo(prices[i]));
                Assert.That(trade.TryUpgrade(quote), Is.True);
                Assert.That(shovel.Level, Is.EqualTo(i + 2));
                Assert.That(wallet.Balance, Is.EqualTo(before - prices[i]));
                Assert.That(trade.TryUpgrade(quote), Is.False);
            }
            Assert.That(wallet.Balance, Is.Zero);
            Assert.That(trade.Check(trade.OfferUpgrade()), Is.EqualTo(TradeResult.Complete));
            Assert.That(trade.TryUpgrade(trade.OfferUpgrade()), Is.False);
        }

        [Test]
        public void ExternalProgressionCannotRewriteAnOffer()
        {
            var shop = new StationTrade(bag, wallet, shovel);
            wallet.TryCredit(50);
            var quote = shop.OfferUpgrade();
            Assert.That(quote.Cost, Is.EqualTo(10));
            shovel.TryUpgradeTo(2);
            Assert.That(shop.TryUpgrade(quote), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(50));
            Assert.That(shovel.Level, Is.EqualTo(2));
        }

        [TestCase(EquipmentKind.Inventory)]
        [TestCase(EquipmentKind.Fuel)]
        public void CapacityTracksAreIndependentSequentialAndPreserveItemsAndCharge(EquipmentKind kind)
        {
            var fuel = new Battery(100);
            fuel.TrySpend(73.5f);
            trade = new StationTrade(bag, wallet, shovel, fuel);
            wallet.TryCredit(Enumerable.Range(1, EquipmentProgression.LevelCount - 1).Sum(EquipmentProgression.Price));
            var item = bag.Items[0];
            int[] capacities = kind == EquipmentKind.Inventory ? new[] { 15, 20, 30, 40, 55, 75, 100, 130, 170 } : new[] { 150, 200, 300, 400, 550, 750, 1000, 1300, 1700 };
            for (int i = 0; i < EquipmentProgression.LevelCount - 1; i++)
            {
                var offer = trade.OfferUpgrade(kind);
                Assert.That(trade.TryUpgrade(offer), Is.True);
                Assert.That(trade.TryUpgrade(offer), Is.False);
                Assert.That(kind == EquipmentKind.Inventory ? bag.Capacity : fuel.Capacity, Is.EqualTo(capacities[i]));
                Assert.That(fuel.Charge, Is.EqualTo(26.5f));
                Assert.That(bag.Items[0], Is.SameAs(item));
                Assert.That(shovel.Level, Is.EqualTo(1));
            }
            Assert.That(wallet.Balance, Is.Zero);
            Assert.That(trade.Check(trade.OfferUpgrade(kind)), Is.EqualTo(TradeResult.Complete));
            Assert.That(kind == EquipmentKind.Inventory ? fuel.Level : bag.Level, Is.EqualTo(1));
        }

        [TestCase(100, 10, 100, 1)]
        [TestCase(.125f, 10, .125f, 1)]
        [TestCase(25, 10, 25, 1)]
        [TestCase(50, 10, 50, 1)]
        [TestCase(85, 10, 85, 1)]
        [TestCase(100.25f, 10, 100.25f, 2)]
        [TestCase(150, 10, 150, 2)]
        [TestCase(200, 10, 200, 2)]
        [TestCase(149, 1, 100, 1)]
        [TestCase(400, 10, 400, 4)]
        [TestCase(99, 0, 0, 0)]
        [TestCase(0, 10, 0, 0)]
        public void RefillQuotesExactAffordableAmountAndNeverChargesTwice(float missing, int credits, float added, double price)
        {
            decimal cost = (decimal)price;
            var fuel = new Battery(400);
            fuel.TrySpend(missing);
            wallet.TryCredit(credits);
            trade = new StationTrade(bag, wallet, shovel, fuel);
            var offer = trade.OfferRefill();
            Assert.That(offer.Amount, Is.EqualTo(added));
            Assert.That(offer.Cost, Is.EqualTo(cost));
            Assert.That(fuel.Charge, Is.EqualTo(400 - missing), "Browsing cannot refuel or bill.");
            Assert.That(trade.TryRefill(offer), Is.EqualTo(cost > 0));
            Assert.That(fuel.Charge, Is.EqualTo(400 - missing + added));
            Assert.That(wallet.Balance, Is.EqualTo(credits - cost));
            Assert.That(trade.TryRefill(offer), Is.False);
            Assert.That(bag.Count, Is.EqualTo(2));
        }

        [Test]
        public void RefillsRejectChangedChargeCapacityWalletAndForeignQuotes()
        {
            var fuel = new Battery(100);
            fuel.TrySpend(60);
            wallet.TryCredit(20);
            trade = new StationTrade(bag, wallet, shovel, fuel);
            var chargeOffer = trade.OfferRefill();
            fuel.TrySpend(1); fuel.TryAdd(1);
            Assert.That(trade.TryRefill(chargeOffer), Is.False);
            var capacityOffer = trade.OfferRefill();
            Assert.That(trade.TryUpgrade(trade.OfferUpgrade(EquipmentKind.Fuel)), Is.True);
            Assert.That(trade.TryRefill(capacityOffer), Is.False);
            var walletOffer = trade.OfferRefill();
            wallet.TryCredit(1); wallet.TrySpend(1);
            Assert.That(trade.TryRefill(walletOffer), Is.False);
            var foreign = new StationTrade(bag, wallet, shovel, fuel);
            Assert.That(foreign.TryRefill(trade.OfferRefill()), Is.False);
            var fresh = trade.OfferRefill();
            Assert.That(fresh.Amount, Is.EqualTo(110));
            Assert.That(fresh.Cost, Is.EqualTo(2));
            Assert.That(trade.TryRefill(fresh), Is.True);
            Assert.That(fuel.Charge, Is.EqualTo(150));
        }

        [Test]
        public void FractionalCriticalFuelAlwaysFillsBeforeChargingAcrossTankSizes()
        {
            var random = new System.Random(142);
            foreach (float capacity in new[] { 100f, 150f, 200f, 300f, 400f })
                for (int i = 0; i < 1000; i++)
                {
                    float charge = (float)random.NextDouble() * capacity * .15f;
                    var fuel = new Battery(capacity); fuel.RestoreCharge(charge);
                    var credits = new SessionWallet(10);
                    var service = new StationTrade(bag, credits, shovel, fuel);
                    var offer = service.OfferRefill();
                    Assert.That(offer.ChargeAfter, Is.EqualTo(capacity));
                    Assert.That(service.TryRefill(offer), Is.True);
                    Assert.That(fuel.Charge, Is.EqualTo(capacity), $"Critical charge {charge:R} must fill.");
                    Assert.That(credits.Balance, Is.EqualTo(10 - offer.Cost));
                    Assert.That(service.TryRefill(offer), Is.False);
                    Assert.That(service.TryRefill(service.OfferRefill()), Is.False);
                    Assert.That(credits.Balance, Is.EqualTo(10 - offer.Cost));
                    fuel.RestoreCharge(charge); fuel.Recharge();
                    Assert.That(fuel.Charge, Is.EqualTo(capacity), "Nonpaid refill also avoids subtraction roundoff.");
                }
        }

        [Test]
        public void PartialRefillsUseWholeBudgetAndMinimumPaymentCannotBeChargedWithoutFuel()
        {
            var fuel = new Battery(150); fuel.RestoreCharge(13.00586f);
            var credits = new SessionWallet(1);
            var service = new StationTrade(bag, credits, shovel, fuel);
            var offer = service.OfferRefill();
            Assert.That(offer.Partial, Is.True);
            Assert.That(offer.Cost, Is.EqualTo(1));
            Assert.That(offer.Amount, Is.EqualTo(100).Within(.00001f));
            Assert.That(service.TryRefill(offer), Is.True);
            Assert.That(fuel.Charge, Is.EqualTo(113.00586f).Within(.00001f));
            Assert.That(credits.Balance, Is.Zero);
            Assert.That(service.TryRefill(service.OfferRefill()), Is.False);
            credits.TryCredit(1);
            Assert.That(service.TryRefill(service.OfferRefill()), Is.True);
            Assert.That(fuel.Charge, Is.EqualTo(150));
            Assert.That(credits.Balance, Is.Zero);
        }
    }
}
