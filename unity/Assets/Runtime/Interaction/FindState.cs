namespace SomethingDownThere
{
    public enum DiscoveryKind { Common, Unique }
    public enum RecoveryMethod { Bag, Rope }
    // Sealed (115): inside an unbroken cavern crystal, out of the world (inactive: no physics, pickup or detector)
    // until its crystal breaks and it falls out as an ordinary find (BuriedFind.Unseal).
    public enum FindState { World, Extracting, Collected, Stored, Sealed }
}
