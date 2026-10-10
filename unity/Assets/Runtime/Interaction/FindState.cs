namespace SomethingDownThere
{
    public enum DiscoveryKind { Common, Unique }
    // Carry (119): a unique small enough for the bag, taken by hand (no slot, never sold) and set down at its stash spot.
    public enum RecoveryMethod { Bag, Rope, Carry }
    // Carried: a carry unique taken from the ground and not yet set down; Stored: a unique kept at camp.
    public enum FindState { World, Extracting, Collected, Stored, Carried }
}
