namespace Resource_Management_System.Common
{
    /// <summary>
    /// Centralized helper for displaying paper-bundle quantities with the
    /// correct Arabic unit word: a quantity of exactly 1 is "رزمة" (singular),
    /// while 2 or more (or 0) is "رزم" (plural) — e.g. "1 رزمة" / "4 رزم".
    /// Use this everywhere a bundle quantity is rendered instead of a
    /// hardcoded "رزمة" string, so plurals stay consistent across the app.
    /// </summary>
    public static class QuantityHelper
    {
        /// <summary>Returns just the unit word ("رزمة" or "رزم") for the given quantity.</summary>
        public static string RezmaUnit(int quantity) => quantity == 1 ? "رزمة" : "رزم";

        /// <summary>Returns the full "{quantity} {unit}" text, e.g. "1 رزمة" or "4 رزم".</summary>
        public static string RezmaText(int quantity) => $"{quantity} {RezmaUnit(quantity)}";
    }
}
