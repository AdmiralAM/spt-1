using AdmiralTrader.Server;
if (BeltProgressionRuntime.BeltPriceFloor(4) != 100000 || BeltProgressionRuntime.BeltPriceFloor(8) != 170000 || BeltProgressionRuntime.BeltPriceFloor(12) != 240000 || BeltProgressionRuntime.BeltPriceFloor(15) != 325000 || BeltProgressionRuntime.BeltPriceFloor(20) != 450000) throw new Exception("belt band drift");
Console.WriteLine("PASS Belt runtime bands");
