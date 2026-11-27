// Its asmdef references only PlainDomain and explicitly disables Unity engine references.
public static class SODataNoEngineCaller
{
    public static void Credit(PlainMoney target, int amount) => target.money += amount;
}
