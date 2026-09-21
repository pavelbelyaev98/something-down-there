using System;
using NUnit.Framework;

namespace SomethingDownThere.Tests
{
    public sealed class RescueTransactionTests
    {
        [TestCase(0, 0)]
        [TestCase(4, 4)]
        [TestCase(10, 10)]
        [TestCase(25, 10)]
        public void ConfirmationAppliesTheShownLossAndClampedFeeOnlyOnce(int balance, int fee)
        {
            var inventory = new SessionInventory();
            var wallet = new SessionWallet(balance);
            var first = new InventoryItem("a", "Marble", 5);
            var second = new InventoryItem("b", "Bead", 11);
            inventory.TryAdd(first);
            inventory.TryAdd(second);
            var rescue = new RescueController(inventory, wallet);
            Assert.That(rescue.TryConfirm(out _), Is.False);
            rescue.Prepare();
            var quote = rescue.Quote;
            Assert.That(quote.LostItems, Is.EqualTo(new[] { first, second }));
            Assert.That(quote.LostSaleValue, Is.EqualTo(16));
            Assert.That(quote.Fee, Is.EqualTo(fee));
            Assert.That(inventory.Count, Is.EqualTo(2), "Preparation must not discard anything.");
            Assert.That(wallet.Balance, Is.EqualTo(balance));
            Assert.That(rescue.TryConfirm(out var receipt), Is.True);
            Assert.That(receipt, Is.SameAs(quote));
            Assert.That(wallet.Balance, Is.EqualTo(quote.RemainingBalance));
            Assert.That(inventory.Count, Is.Zero);
            Assert.That(rescue.TryConfirm(out _), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(balance - fee));
        }

        [TestCase("credits")]
        [TestCase("added item")]
        [TestCase("replaced identity")]
        public void ChangedLossesRequireANewPreviewBeforeAnyMutation(string change)
        {
            var inventory = new SessionInventory();
            var wallet = new SessionWallet(25);
            inventory.TryAdd(new InventoryItem("a", "Marble", 5));
            var rescue = new RescueController(inventory, wallet);
            rescue.Prepare();
            if (change == "credits") wallet.TrySpend(1);
            else if (change == "added item") inventory.TryAdd(new InventoryItem("b", "Bead", 11));
            else
            {
                inventory.TryRemove("a", out _);
                inventory.TryAdd(new InventoryItem("a", "Replacement", 90));
            }
            int count = inventory.Count;
            decimal balance = wallet.Balance;
            Assert.That(rescue.TryConfirm(out _), Is.False);
            Assert.That(inventory.Count, Is.EqualTo(count));
            Assert.That(wallet.Balance, Is.EqualTo(balance));
            rescue.Prepare();
            Assert.That(rescue.TryConfirm(out _), Is.True);
            Assert.That(wallet.Balance, Is.EqualTo(balance - 10));
        }

        [Test]
        public void CancelInvalidatesConfirmationAndLargeItemValuesDoNotOverflow()
        {
            var inventory = new SessionInventory();
            inventory.TryAdd(new InventoryItem("a", "First", int.MaxValue));
            inventory.TryAdd(new InventoryItem("b", "Second", int.MaxValue));
            var wallet = new SessionWallet(30);
            var rescue = new RescueController(inventory, wallet, 7);
            rescue.Prepare();
            Assert.That(rescue.Quote.LostSaleValue, Is.EqualTo(2L * int.MaxValue));
            Assert.That(rescue.Quote.Fee, Is.EqualTo(7));
            rescue.Cancel();
            Assert.That(rescue.TryConfirm(out _), Is.False);
            Assert.That(inventory.Count, Is.EqualTo(2));
            Assert.That(wallet.Balance, Is.EqualTo(30));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RescueController(inventory, wallet, -1));
        }

        [Test]
        public void WalletRejectsNegativeOverdrawnAndOverflowingTransactionsWithoutMutation()
        {
            var wallet = new SessionWallet(8);
            Assert.That(wallet.TrySpend(-1), Is.False);
            Assert.That(wallet.TrySpend(9), Is.False);
            Assert.That(wallet.TryCredit(-1), Is.False);
            Assert.That(wallet.TryCredit(int.MaxValue), Is.False);
            Assert.That(wallet.TrySpend(.01m), Is.False);
            Assert.That(wallet.TryCredit(.01m), Is.False);
            Assert.That(wallet.Balance, Is.EqualTo(8));
            Assert.That(wallet.TryCredit(4), Is.True);
            Assert.That(wallet.TrySpend(12), Is.True);
            Assert.That(wallet.Balance, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => new SessionWallet(-1));
        }

    }
}
