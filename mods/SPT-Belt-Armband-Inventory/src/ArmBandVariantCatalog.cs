using System;
using System.Collections.Generic;
using System.Linq;

namespace SPTBeltArmbandInventory;

public enum ArmBandRole { Medical, Ammo, Magazine, Technical, Currency }
public enum ArmBandVisualPool { ExistingRaid, Standard, All }

public sealed class ArmBandVariantDescriptor
{
    public string SourceTemplateId { get; }
    public string VisualKey { get; }
    public ArmBandVisualPool VisualPool { get; }
    public ArmBandRole Role { get; }
    public string TemplateId { get; }
    public string GridId { get; }

    public ArmBandVariantDescriptor(
        string sourceTemplateId,
        string visualKey,
        ArmBandVisualPool visualPool,
        ArmBandRole role,
        string templateId,
        string gridId)
    {
        SourceTemplateId = sourceTemplateId;
        VisualKey = visualKey;
        VisualPool = visualPool;
        Role = role;
        TemplateId = templateId;
        GridId = gridId;
    }
}

public static class ArmBandVariantCatalog
{
    public static readonly ArmBandVariantDescriptor[] All =
    [
        new("5b3f16c486f7747c327f55f7", "White", ArmBandVisualPool.Standard, ArmBandRole.Medical, "68ad00000000000000000001", "68ae00000000000000000001"),
        new("5b3f16c486f7747c327f55f7", "White", ArmBandVisualPool.Standard, ArmBandRole.Ammo, "68ad00000000000000000002", "68ae00000000000000000002"),
        new("5b3f16c486f7747c327f55f7", "White", ArmBandVisualPool.Standard, ArmBandRole.Magazine, "68ad00000000000000000003", "68ae00000000000000000003"),
        new("5b3f16c486f7747c327f55f7", "White", ArmBandVisualPool.Standard, ArmBandRole.Technical, "68ad00000000000000000004", "68ae00000000000000000004"),
        new("5b3f16c486f7747c327f55f7", "White", ArmBandVisualPool.Standard, ArmBandRole.Currency, "68ad00000000000000000005", "68ae00000000000000000005"),
        new("5b3f3ade86f7746b6b790d8e", "Red", ArmBandVisualPool.Standard, ArmBandRole.Medical, "68ad00000000000000000006", "68ae00000000000000000006"),
        new("5b3f3ade86f7746b6b790d8e", "Red", ArmBandVisualPool.Standard, ArmBandRole.Ammo, "68ad00000000000000000007", "68ae00000000000000000007"),
        new("5b3f3ade86f7746b6b790d8e", "Red", ArmBandVisualPool.Standard, ArmBandRole.Magazine, "68ad00000000000000000008", "68ae00000000000000000008"),
        new("5b3f3ade86f7746b6b790d8e", "Red", ArmBandVisualPool.Standard, ArmBandRole.Technical, "68ad00000000000000000009", "68ae00000000000000000009"),
        new("5b3f3ade86f7746b6b790d8e", "Red", ArmBandVisualPool.Standard, ArmBandRole.Currency, "68ad0000000000000000000a", "68ae0000000000000000000a"),
        new("5b3f3af486f774679e752c1f", "Blue", ArmBandVisualPool.Standard, ArmBandRole.Medical, "68ad0000000000000000000b", "68ae0000000000000000000b"),
        new("5b3f3af486f774679e752c1f", "Blue", ArmBandVisualPool.Standard, ArmBandRole.Ammo, "68ad0000000000000000000c", "68ae0000000000000000000c"),
        new("5b3f3af486f774679e752c1f", "Blue", ArmBandVisualPool.Standard, ArmBandRole.Magazine, "68ad0000000000000000000d", "68ae0000000000000000000d"),
        new("5b3f3af486f774679e752c1f", "Blue", ArmBandVisualPool.Standard, ArmBandRole.Technical, "68ad0000000000000000000e", "68ae0000000000000000000e"),
        new("5b3f3af486f774679e752c1f", "Blue", ArmBandVisualPool.Standard, ArmBandRole.Currency, "68ad0000000000000000000f", "68ae0000000000000000000f"),
        new("5b3f3b0186f774021a2afef7", "Green", ArmBandVisualPool.Standard, ArmBandRole.Medical, "68ad00000000000000000010", "68ae00000000000000000010"),
        new("5b3f3b0186f774021a2afef7", "Green", ArmBandVisualPool.Standard, ArmBandRole.Ammo, "68ad00000000000000000011", "68ae00000000000000000011"),
        new("5b3f3b0186f774021a2afef7", "Green", ArmBandVisualPool.Standard, ArmBandRole.Magazine, "68ad00000000000000000012", "68ae00000000000000000012"),
        new("5b3f3b0186f774021a2afef7", "Green", ArmBandVisualPool.Standard, ArmBandRole.Technical, "68ad00000000000000000013", "68ae00000000000000000013"),
        new("5b3f3b0186f774021a2afef7", "Green", ArmBandVisualPool.Standard, ArmBandRole.Currency, "68ad00000000000000000014", "68ae00000000000000000014"),
        new("5b3f3b0e86f7746752107cda", "Yellow", ArmBandVisualPool.Standard, ArmBandRole.Medical, "68ad00000000000000000015", "68ae00000000000000000015"),
        new("5b3f3b0e86f7746752107cda", "Yellow", ArmBandVisualPool.Standard, ArmBandRole.Ammo, "68ad00000000000000000016", "68ae00000000000000000016"),
        new("5b3f3b0e86f7746752107cda", "Yellow", ArmBandVisualPool.Standard, ArmBandRole.Magazine, "68ad00000000000000000017", "68ae00000000000000000017"),
        new("5b3f3b0e86f7746752107cda", "Yellow", ArmBandVisualPool.Standard, ArmBandRole.Technical, "68ad00000000000000000018", "68ae00000000000000000018"),
        new("5b3f3b0e86f7746752107cda", "Yellow", ArmBandVisualPool.Standard, ArmBandRole.Currency, "68ad00000000000000000019", "68ae00000000000000000019"),
        new("5f9949d869e2777a0e779ba5", "Rivals2020", ArmBandVisualPool.ExistingRaid, ArmBandRole.Medical, "68ad0000000000000000001a", "68ae0000000000000000001a"),
        new("5f9949d869e2777a0e779ba5", "Rivals2020", ArmBandVisualPool.ExistingRaid, ArmBandRole.Ammo, "68ad0000000000000000001b", "68ae0000000000000000001b"),
        new("5f9949d869e2777a0e779ba5", "Rivals2020", ArmBandVisualPool.ExistingRaid, ArmBandRole.Magazine, "68ad0000000000000000001c", "68ae0000000000000000001c"),
        new("5f9949d869e2777a0e779ba5", "Rivals2020", ArmBandVisualPool.ExistingRaid, ArmBandRole.Technical, "68ad0000000000000000001d", "68ae0000000000000000001d"),
        new("5f9949d869e2777a0e779ba5", "Rivals2020", ArmBandVisualPool.ExistingRaid, ArmBandRole.Currency, "68ad0000000000000000001e", "68ae0000000000000000001e"),
        new("60b0f988c4449e4cb624c1da", "Evasion", ArmBandVisualPool.ExistingRaid, ArmBandRole.Medical, "68ad0000000000000000001f", "68ae0000000000000000001f"),
        new("60b0f988c4449e4cb624c1da", "Evasion", ArmBandVisualPool.ExistingRaid, ArmBandRole.Ammo, "68ad00000000000000000020", "68ae00000000000000000020"),
        new("60b0f988c4449e4cb624c1da", "Evasion", ArmBandVisualPool.ExistingRaid, ArmBandRole.Magazine, "68ad00000000000000000021", "68ae00000000000000000021"),
        new("60b0f988c4449e4cb624c1da", "Evasion", ArmBandVisualPool.ExistingRaid, ArmBandRole.Technical, "68ad00000000000000000022", "68ae00000000000000000022"),
        new("60b0f988c4449e4cb624c1da", "Evasion", ArmBandVisualPool.ExistingRaid, ArmBandRole.Currency, "68ad00000000000000000023", "68ae00000000000000000023"),
        new("619bc61e86e01e16f839a999", "Alpha", ArmBandVisualPool.All, ArmBandRole.Medical, "68ad00000000000000000024", "68ae00000000000000000024"),
        new("619bc61e86e01e16f839a999", "Alpha", ArmBandVisualPool.All, ArmBandRole.Ammo, "68ad00000000000000000025", "68ae00000000000000000025"),
        new("619bc61e86e01e16f839a999", "Alpha", ArmBandVisualPool.All, ArmBandRole.Magazine, "68ad00000000000000000026", "68ae00000000000000000026"),
        new("619bc61e86e01e16f839a999", "Alpha", ArmBandVisualPool.All, ArmBandRole.Technical, "68ad00000000000000000027", "68ae00000000000000000027"),
        new("619bc61e86e01e16f839a999", "Alpha", ArmBandVisualPool.All, ArmBandRole.Currency, "68ad00000000000000000028", "68ae00000000000000000028"),
        new("619bdd8886e01e16f839a99c", "BEAR", ArmBandVisualPool.ExistingRaid, ArmBandRole.Medical, "68ad00000000000000000029", "68ae00000000000000000029"),
        new("619bdd8886e01e16f839a99c", "BEAR", ArmBandVisualPool.ExistingRaid, ArmBandRole.Ammo, "68ad0000000000000000002a", "68ae0000000000000000002a"),
        new("619bdd8886e01e16f839a99c", "BEAR", ArmBandVisualPool.ExistingRaid, ArmBandRole.Magazine, "68ad0000000000000000002b", "68ae0000000000000000002b"),
        new("619bdd8886e01e16f839a99c", "BEAR", ArmBandVisualPool.ExistingRaid, ArmBandRole.Technical, "68ad0000000000000000002c", "68ae0000000000000000002c"),
        new("619bdd8886e01e16f839a99c", "BEAR", ArmBandVisualPool.ExistingRaid, ArmBandRole.Currency, "68ad0000000000000000002d", "68ae0000000000000000002d"),
        new("619bddc6c9546643a67df6ee", "DEADSKUL", ArmBandVisualPool.Standard, ArmBandRole.Medical, "68ad0000000000000000002e", "68ae0000000000000000002e"),
        new("619bddc6c9546643a67df6ee", "DEADSKUL", ArmBandVisualPool.Standard, ArmBandRole.Ammo, "68ad0000000000000000002f", "68ae0000000000000000002f"),
        new("619bddc6c9546643a67df6ee", "DEADSKUL", ArmBandVisualPool.Standard, ArmBandRole.Magazine, "68ad00000000000000000030", "68ae00000000000000000030"),
        new("619bddc6c9546643a67df6ee", "DEADSKUL", ArmBandVisualPool.Standard, ArmBandRole.Technical, "68ad00000000000000000031", "68ae00000000000000000031"),
        new("619bddc6c9546643a67df6ee", "DEADSKUL", ArmBandVisualPool.Standard, ArmBandRole.Currency, "68ad00000000000000000032", "68ae00000000000000000032"),
        new("619bddffc9546643a67df6f0", "TrainHard", ArmBandVisualPool.Standard, ArmBandRole.Medical, "68ad00000000000000000033", "68ae00000000000000000033"),
        new("619bddffc9546643a67df6f0", "TrainHard", ArmBandVisualPool.Standard, ArmBandRole.Ammo, "68ad00000000000000000034", "68ae00000000000000000034"),
        new("619bddffc9546643a67df6f0", "TrainHard", ArmBandVisualPool.Standard, ArmBandRole.Magazine, "68ad00000000000000000035", "68ae00000000000000000035"),
        new("619bddffc9546643a67df6f0", "TrainHard", ArmBandVisualPool.Standard, ArmBandRole.Technical, "68ad00000000000000000036", "68ae00000000000000000036"),
        new("619bddffc9546643a67df6f0", "TrainHard", ArmBandVisualPool.Standard, ArmBandRole.Currency, "68ad00000000000000000037", "68ae00000000000000000037"),
        new("619bde3dc9546643a67df6f2", "KibaArms", ArmBandVisualPool.Standard, ArmBandRole.Medical, "68ad00000000000000000038", "68ae00000000000000000038"),
        new("619bde3dc9546643a67df6f2", "KibaArms", ArmBandVisualPool.Standard, ArmBandRole.Ammo, "68ad00000000000000000039", "68ae00000000000000000039"),
        new("619bde3dc9546643a67df6f2", "KibaArms", ArmBandVisualPool.Standard, ArmBandRole.Magazine, "68ad0000000000000000003a", "68ae0000000000000000003a"),
        new("619bde3dc9546643a67df6f2", "KibaArms", ArmBandVisualPool.Standard, ArmBandRole.Technical, "68ad0000000000000000003b", "68ae0000000000000000003b"),
        new("619bde3dc9546643a67df6f2", "KibaArms", ArmBandVisualPool.Standard, ArmBandRole.Currency, "68ad0000000000000000003c", "68ae0000000000000000003c"),
        new("619bde7fc9546643a67df6f4", "Labs", ArmBandVisualPool.ExistingRaid, ArmBandRole.Medical, "68ad0000000000000000003d", "68ae0000000000000000003d"),
        new("619bde7fc9546643a67df6f4", "Labs", ArmBandVisualPool.ExistingRaid, ArmBandRole.Ammo, "68ad0000000000000000003e", "68ae0000000000000000003e"),
        new("619bde7fc9546643a67df6f4", "Labs", ArmBandVisualPool.ExistingRaid, ArmBandRole.Magazine, "68ad0000000000000000003f", "68ae0000000000000000003f"),
        new("619bde7fc9546643a67df6f4", "Labs", ArmBandVisualPool.ExistingRaid, ArmBandRole.Technical, "68ad00000000000000000040", "68ae00000000000000000040"),
        new("619bde7fc9546643a67df6f4", "Labs", ArmBandVisualPool.ExistingRaid, ArmBandRole.Currency, "68ad00000000000000000041", "68ae00000000000000000041"),
        new("619bdeb986e01e16f839a99e", "RFARMY", ArmBandVisualPool.Standard, ArmBandRole.Medical, "68ad00000000000000000042", "68ae00000000000000000042"),
        new("619bdeb986e01e16f839a99e", "RFARMY", ArmBandVisualPool.Standard, ArmBandRole.Ammo, "68ad00000000000000000043", "68ae00000000000000000043"),
        new("619bdeb986e01e16f839a99e", "RFARMY", ArmBandVisualPool.Standard, ArmBandRole.Magazine, "68ad00000000000000000044", "68ae00000000000000000044"),
        new("619bdeb986e01e16f839a99e", "RFARMY", ArmBandVisualPool.Standard, ArmBandRole.Technical, "68ad00000000000000000045", "68ae00000000000000000045"),
        new("619bdeb986e01e16f839a99e", "RFARMY", ArmBandVisualPool.Standard, ArmBandRole.Currency, "68ad00000000000000000046", "68ae00000000000000000046"),
        new("619bdef8c9546643a67df6f6", "TerraGroup", ArmBandVisualPool.ExistingRaid, ArmBandRole.Medical, "68ad00000000000000000047", "68ae00000000000000000047"),
        new("619bdef8c9546643a67df6f6", "TerraGroup", ArmBandVisualPool.ExistingRaid, ArmBandRole.Ammo, "68ad00000000000000000048", "68ae00000000000000000048"),
        new("619bdef8c9546643a67df6f6", "TerraGroup", ArmBandVisualPool.ExistingRaid, ArmBandRole.Magazine, "68ad00000000000000000049", "68ae00000000000000000049"),
        new("619bdef8c9546643a67df6f6", "TerraGroup", ArmBandVisualPool.ExistingRaid, ArmBandRole.Technical, "68ad0000000000000000004a", "68ae0000000000000000004a"),
        new("619bdef8c9546643a67df6f6", "TerraGroup", ArmBandVisualPool.ExistingRaid, ArmBandRole.Currency, "68ad0000000000000000004b", "68ae0000000000000000004b"),
        new("619bdf9cc9546643a67df6f8", "UNTAR", ArmBandVisualPool.Standard, ArmBandRole.Medical, "68ad0000000000000000004c", "68ae0000000000000000004c"),
        new("619bdf9cc9546643a67df6f8", "UNTAR", ArmBandVisualPool.Standard, ArmBandRole.Ammo, "68ad0000000000000000004d", "68ae0000000000000000004d"),
        new("619bdf9cc9546643a67df6f8", "UNTAR", ArmBandVisualPool.Standard, ArmBandRole.Magazine, "68ad0000000000000000004e", "68ae0000000000000000004e"),
        new("619bdf9cc9546643a67df6f8", "UNTAR", ArmBandVisualPool.Standard, ArmBandRole.Technical, "68ad0000000000000000004f", "68ae0000000000000000004f"),
        new("619bdf9cc9546643a67df6f8", "UNTAR", ArmBandVisualPool.Standard, ArmBandRole.Currency, "68ad00000000000000000050", "68ae00000000000000000050"),
        new("619bdfd4c9546643a67df6fa", "USEC", ArmBandVisualPool.ExistingRaid, ArmBandRole.Medical, "68ad00000000000000000051", "68ae00000000000000000051"),
        new("619bdfd4c9546643a67df6fa", "USEC", ArmBandVisualPool.ExistingRaid, ArmBandRole.Ammo, "68ad00000000000000000052", "68ae00000000000000000052"),
        new("619bdfd4c9546643a67df6fa", "USEC", ArmBandVisualPool.ExistingRaid, ArmBandRole.Magazine, "68ad00000000000000000053", "68ae00000000000000000053"),
        new("619bdfd4c9546643a67df6fa", "USEC", ArmBandVisualPool.ExistingRaid, ArmBandRole.Technical, "68ad00000000000000000054", "68ae00000000000000000054"),
        new("619bdfd4c9546643a67df6fa", "USEC", ArmBandVisualPool.ExistingRaid, ArmBandRole.Currency, "68ad00000000000000000055", "68ae00000000000000000055"),
        new("660312cc4d6cdfa6f500c703", "TheUnheard", ArmBandVisualPool.All, ArmBandRole.Medical, "68ad00000000000000000056", "68ae00000000000000000056"),
        new("660312cc4d6cdfa6f500c703", "TheUnheard", ArmBandVisualPool.All, ArmBandRole.Ammo, "68ad00000000000000000057", "68ae00000000000000000057"),
        new("660312cc4d6cdfa6f500c703", "TheUnheard", ArmBandVisualPool.All, ArmBandRole.Magazine, "68ad00000000000000000058", "68ae00000000000000000058"),
        new("660312cc4d6cdfa6f500c703", "TheUnheard", ArmBandVisualPool.All, ArmBandRole.Technical, "68ad00000000000000000059", "68ae00000000000000000059"),
        new("660312cc4d6cdfa6f500c703", "TheUnheard", ArmBandVisualPool.All, ArmBandRole.Currency, "68ad0000000000000000005a", "68ae0000000000000000005a"),
        new("664a5480bfcc521bad3192ca", "ARENA", ArmBandVisualPool.All, ArmBandRole.Medical, "68ad0000000000000000005b", "68ae0000000000000000005b"),
        new("664a5480bfcc521bad3192ca", "ARENA", ArmBandVisualPool.All, ArmBandRole.Ammo, "68ad0000000000000000005c", "68ae0000000000000000005c"),
        new("664a5480bfcc521bad3192ca", "ARENA", ArmBandVisualPool.All, ArmBandRole.Magazine, "68ad0000000000000000005d", "68ae0000000000000000005d"),
        new("664a5480bfcc521bad3192ca", "ARENA", ArmBandVisualPool.All, ArmBandRole.Technical, "68ad0000000000000000005e", "68ae0000000000000000005e"),
        new("664a5480bfcc521bad3192ca", "ARENA", ArmBandVisualPool.All, ArmBandRole.Currency, "68ad0000000000000000005f", "68ae0000000000000000005f"),
        new("67614b3ab8c060ebb204b106", "Khorovod", ArmBandVisualPool.All, ArmBandRole.Medical, "68ad00000000000000000060", "68ae00000000000000000060"),
        new("67614b3ab8c060ebb204b106", "Khorovod", ArmBandVisualPool.All, ArmBandRole.Ammo, "68ad00000000000000000061", "68ae00000000000000000061"),
        new("67614b3ab8c060ebb204b106", "Khorovod", ArmBandVisualPool.All, ArmBandRole.Magazine, "68ad00000000000000000062", "68ae00000000000000000062"),
        new("67614b3ab8c060ebb204b106", "Khorovod", ArmBandVisualPool.All, ArmBandRole.Technical, "68ad00000000000000000063", "68ae00000000000000000063"),
        new("67614b3ab8c060ebb204b106", "Khorovod", ArmBandVisualPool.All, ArmBandRole.Currency, "68ad00000000000000000064", "68ae00000000000000000064"),
        new("67614b542eb91250020f2b86", "Prestige1", ArmBandVisualPool.All, ArmBandRole.Medical, "68ad00000000000000000065", "68ae00000000000000000065"),
        new("67614b542eb91250020f2b86", "Prestige1", ArmBandVisualPool.All, ArmBandRole.Ammo, "68ad00000000000000000066", "68ae00000000000000000066"),
        new("67614b542eb91250020f2b86", "Prestige1", ArmBandVisualPool.All, ArmBandRole.Magazine, "68ad00000000000000000067", "68ae00000000000000000067"),
        new("67614b542eb91250020f2b86", "Prestige1", ArmBandVisualPool.All, ArmBandRole.Technical, "68ad00000000000000000068", "68ae00000000000000000068"),
        new("67614b542eb91250020f2b86", "Prestige1", ArmBandVisualPool.All, ArmBandRole.Currency, "68ad00000000000000000069", "68ae00000000000000000069"),
        new("67614b6b47c71ea3d40256d7", "Prestige2", ArmBandVisualPool.All, ArmBandRole.Medical, "68ad0000000000000000006a", "68ae0000000000000000006a"),
        new("67614b6b47c71ea3d40256d7", "Prestige2", ArmBandVisualPool.All, ArmBandRole.Ammo, "68ad0000000000000000006b", "68ae0000000000000000006b"),
        new("67614b6b47c71ea3d40256d7", "Prestige2", ArmBandVisualPool.All, ArmBandRole.Magazine, "68ad0000000000000000006c", "68ae0000000000000000006c"),
        new("67614b6b47c71ea3d40256d7", "Prestige2", ArmBandVisualPool.All, ArmBandRole.Technical, "68ad0000000000000000006d", "68ae0000000000000000006d"),
        new("67614b6b47c71ea3d40256d7", "Prestige2", ArmBandVisualPool.All, ArmBandRole.Currency, "68ad0000000000000000006e", "68ae0000000000000000006e"),
        new("6841b2506c1fcc41ed0db319", "Prestige3", ArmBandVisualPool.All, ArmBandRole.Medical, "68ad0000000000000000006f", "68ae0000000000000000006f"),
        new("6841b2506c1fcc41ed0db319", "Prestige3", ArmBandVisualPool.All, ArmBandRole.Ammo, "68ad00000000000000000070", "68ae00000000000000000070"),
        new("6841b2506c1fcc41ed0db319", "Prestige3", ArmBandVisualPool.All, ArmBandRole.Magazine, "68ad00000000000000000071", "68ae00000000000000000071"),
        new("6841b2506c1fcc41ed0db319", "Prestige3", ArmBandVisualPool.All, ArmBandRole.Technical, "68ad00000000000000000072", "68ae00000000000000000072"),
        new("6841b2506c1fcc41ed0db319", "Prestige3", ArmBandVisualPool.All, ArmBandRole.Currency, "68ad00000000000000000073", "68ae00000000000000000073"),
        new("6841b3463fcc417de40a6768", "Prestige4", ArmBandVisualPool.All, ArmBandRole.Medical, "68ad00000000000000000074", "68ae00000000000000000074"),
        new("6841b3463fcc417de40a6768", "Prestige4", ArmBandVisualPool.All, ArmBandRole.Ammo, "68ad00000000000000000075", "68ae00000000000000000075"),
        new("6841b3463fcc417de40a6768", "Prestige4", ArmBandVisualPool.All, ArmBandRole.Magazine, "68ad00000000000000000076", "68ae00000000000000000076"),
        new("6841b3463fcc417de40a6768", "Prestige4", ArmBandVisualPool.All, ArmBandRole.Technical, "68ad00000000000000000077", "68ae00000000000000000077"),
        new("6841b3463fcc417de40a6768", "Prestige4", ArmBandVisualPool.All, ArmBandRole.Currency, "68ad00000000000000000078", "68ae00000000000000000078"),
        new("6841b3ab322db20d190b4b9b", "Prestige5", ArmBandVisualPool.All, ArmBandRole.Medical, "68ad00000000000000000079", "68ae00000000000000000079"),
        new("6841b3ab322db20d190b4b9b", "Prestige5", ArmBandVisualPool.All, ArmBandRole.Ammo, "68ad0000000000000000007a", "68ae0000000000000000007a"),
        new("6841b3ab322db20d190b4b9b", "Prestige5", ArmBandVisualPool.All, ArmBandRole.Magazine, "68ad0000000000000000007b", "68ae0000000000000000007b"),
        new("6841b3ab322db20d190b4b9b", "Prestige5", ArmBandVisualPool.All, ArmBandRole.Technical, "68ad0000000000000000007c", "68ae0000000000000000007c"),
        new("6841b3ab322db20d190b4b9b", "Prestige5", ArmBandVisualPool.All, ArmBandRole.Currency, "68ad0000000000000000007d", "68ae0000000000000000007d"),
        new("688b2e574172ca83e70cf868", "Discord", ArmBandVisualPool.All, ArmBandRole.Medical, "68ad0000000000000000007e", "68ae0000000000000000007e"),
        new("688b2e574172ca83e70cf868", "Discord", ArmBandVisualPool.All, ArmBandRole.Ammo, "68ad0000000000000000007f", "68ae0000000000000000007f"),
        new("688b2e574172ca83e70cf868", "Discord", ArmBandVisualPool.All, ArmBandRole.Magazine, "68ad00000000000000000080", "68ae00000000000000000080"),
        new("688b2e574172ca83e70cf868", "Discord", ArmBandVisualPool.All, ArmBandRole.Technical, "68ad00000000000000000081", "68ae00000000000000000081"),
        new("688b2e574172ca83e70cf868", "Discord", ArmBandVisualPool.All, ArmBandRole.Currency, "68ad00000000000000000082", "68ae00000000000000000082"),
    ];

    private static readonly IReadOnlyDictionary<string, ArmBandVariantDescriptor> ByTemplate =
        All.ToDictionary(variant => variant.TemplateId, StringComparer.Ordinal);

    public static bool TryGet(string templateId, out ArmBandVariantDescriptor descriptor)
    {
        if (string.IsNullOrEmpty(templateId))
        {
            descriptor = null!;
            return false;
        }
        return ByTemplate.TryGetValue(templateId, out descriptor!);
    }

    public static bool HasRole(string templateId, ArmBandRole role) =>
        TryGet(templateId, out ArmBandVariantDescriptor descriptor) && descriptor.Role == role;
}
