using AdmiralTrader.Server;
if (BeltProgressionRuntime.BeltPriceFloor(4) != 100000 || BeltProgressionRuntime.BeltPriceFloor(8) != 170000 || BeltProgressionRuntime.BeltPriceFloor(12) != 240000 || BeltProgressionRuntime.BeltPriceFloor(15) != 325000 || BeltProgressionRuntime.BeltPriceFloor(20) != 450000) throw new Exception("belt band drift");
if (BeltProgressionRuntime.BeltLoyaltyLevel(4) != 1 || BeltProgressionRuntime.BeltLoyaltyLevel(8) != 2 || BeltProgressionRuntime.BeltLoyaltyLevel(12) != 3 || BeltProgressionRuntime.BeltLoyaltyLevel(15) != 4) throw new Exception("belt loyalty drift");
if (BeltProgressionRuntime.BeltStock(4) != 3 || BeltProgressionRuntime.BeltStock(12) != 2 || BeltProgressionRuntime.BeltStock(15) != 1) throw new Exception("belt stock drift");
Console.WriteLine("PASS Belt runtime bands");
