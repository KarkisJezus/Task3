using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

class Program
{
    static void Main(string[] args)
    {
        try
        {
            var dice = DiceParser.ParseDice(args);
            var game = new Game(dice);
            game.Start();
        }
        catch (ArgumentException ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine("Example usage: dotnet run 2,2,4,4,9,9 6,8,1,1,8,6 7,5,3,7,5,3");
        }
    }
}
public class Dice
{
    public int[] Faces { get; }

    public Dice(int[] faces)
    {
        Faces = faces;
    }
}

public class DiceParser
{
    public static List<Dice> ParseDice(string[] args)
    {
        if (args.Length < 3)
        {
            throw new ArgumentException("You must provide at least three dice.");
        }

        List<Dice> dice = new List<Dice>();
        foreach (var arg in args)
        {
            var parts = arg.Split(',');
            if (parts.Length != 6 || !parts.All(p => int.TryParse(p, out _)))
            {
                throw new ArgumentException($"Invalid dice configuration: {arg}. Each dice must contain exactly 6 comma-separated integers.");
            }

            dice.Add(new Dice(parts.Select(int.Parse).ToArray()));
        }

        return dice;
    }
}
public class FairRandomGenerator
{
    public static (int computerNumber, byte[] key, byte[] hmac) GenerateFairRandomNumber(int range)
    {
        int computerNumber;
        byte[] randomNumber = new byte[4];
        do
        {
            RandomNumberGenerator.Fill(randomNumber);
            computerNumber = BitConverter.ToInt32(randomNumber, 0) & int.MaxValue;
        } while (computerNumber >= range * (int.MaxValue / range));
        computerNumber %= range;

        var (key, hmac) = HmacCalculator.GenerateHmac(computerNumber);
        return (computerNumber, key, hmac);
    }
}

public class HmacCalculator
{
    public static (byte[] key, byte[] hmac) GenerateHmac(int number)
    {
        using (var hmacSha3 = new HMACSHA256())
        {
            byte[] key = new byte[32];
            RandomNumberGenerator.Fill(key);

            byte[] message = BitConverter.GetBytes(number);
            hmacSha3.Key = key;
            byte[] hmac = hmacSha3.ComputeHash(message);

            return (key, hmac);
        }
    }

    public static bool VerifyHmac(byte[] key, int number, byte[] hmac)
    {
        using (var hmacSha3 = new HMACSHA256(key))
        {
            byte[] message = BitConverter.GetBytes(number);
            byte[] computedHmac = hmacSha3.ComputeHash(message);

            return hmac.SequenceEqual(computedHmac);
        }
    }
}

class ProbabilityCalculator
{
    public static double CalculateWinningProbability(Dice dice1, Dice dice2)
    {
        int wins = 0;
        int total = dice1.Faces.Length * dice2.Faces.Length;

        foreach (var face1 in dice1.Faces)
        {
            foreach (var face2 in dice2.Faces)
            {
                if (face1 > face2)
                {
                    wins++;
                }
            }
        }

        return (double)wins / total;
    }
}

class ProbabilityTable
{
    public static void DisplayProbabilityTable(List<Dice> dice)
    {
        Console.WriteLine("+-------------+-------------+--------------+--------------+---------+\n| Dice 1      | Dice 2      | Dice 1 Wins  | Dice 2 Wins  | Draw    |\n+-------------+-------------+--------------+--------------+---------+");
        for (int i = 0; i < dice.Count; i++)
        {
            for (int j = i + 1; j < dice.Count; j++)
            {
                double win1 = ProbabilityCalculator.CalculateWinningProbability(dice[i], dice[j]) * 100;
                double win2 = ProbabilityCalculator.CalculateWinningProbability(dice[j], dice[i]) * 100;
                double draw = 100 - (win1 + win2);
                Console.WriteLine($"| {string.Join(",", dice[i].Faces),-10} | {string.Join(",", dice[j].Faces),-10} | {win1,11:0.00}% | {win2,11:0.00}% | {draw,4:0.00}%   |");
            }
        }
        Console.WriteLine("+-------------+-------------+--------------+--------------+---------+");
    }
}


public class Game
{
    private List<Dice> dice;
    private Random random = new Random();

    public Game(List<Dice> dice)
    {
        this.dice = dice;
    }

    public void Start()
    {
        Console.WriteLine("\nWelcome to the Non-Transitive Dice Game!");
        Console.WriteLine("Let's determine who makes the first move...");

        var (computerNumber, key, hmac) = FairRandomGenerator.GenerateFairRandomNumber(2);
        Console.WriteLine($"I selected a random value in the range 0..1 (HMAC={BitConverter.ToString(hmac).Replace("-", "")}).");

        Console.WriteLine("Try to guess my selection.");
        Console.WriteLine("0 - 0");
        Console.WriteLine("1 - 1");
        Console.WriteLine("X - exit");
        Console.WriteLine("? - help");

        string? userInput;
        int userNumber;
        while (true)
        {
            Console.Write("Your selection: ");
            userInput = Console.ReadLine();

            if (userInput == null || userInput.ToUpper() == "X")
            {
                return;
            }
            else if (userInput == "?")
            {
                ProbabilityTable.DisplayProbabilityTable(dice);
                continue;
            }

            if (int.TryParse(userInput, out userNumber) && (userNumber == 0 || userNumber == 1))
            {
                break;
            }
            else
            {
                Console.WriteLine("Invalid input. Try again.");
            }
        }

        int result = (userNumber + computerNumber) % 2;
        Console.WriteLine($"My selection: {computerNumber} (KEY={BitConverter.ToString(key).Replace("-", "")}).");
        bool userGoesFirst = result == 0;
        Console.WriteLine(userGoesFirst ? "You go first!" : "I make the first move!");

        int userDiceIndex, computerDiceIndex;

        if (userGoesFirst)
        {
            userDiceIndex = SelectDice("User");
            computerDiceIndex = SelectRandomDice(userDiceIndex);
            Console.WriteLine($"You choose the [{string.Join(",", dice[userDiceIndex].Faces)}] dice.");
            Console.WriteLine($"I chose the [{string.Join(",", dice[computerDiceIndex].Faces)}] dice.");
        }
        else
        {
            computerDiceIndex = SelectRandomDice();
            Console.WriteLine($"I choose the [{string.Join(",", dice[computerDiceIndex].Faces)}] dice.");
            userDiceIndex = SelectDice("User", computerDiceIndex);
            Console.WriteLine($"You chose the [{string.Join(",", dice[userDiceIndex].Faces)}] dice.");
        }

        Console.WriteLine("It's time for my throw.");
        int computerRoll = FairRollDice(dice[computerDiceIndex], "Computer");
        Console.WriteLine("It's time for your throw.");
        int userRoll = FairRollDice(dice[userDiceIndex], "User");

        Console.WriteLine($"My throw is {computerRoll}.");
        Console.WriteLine($"Your throw is {userRoll}.");

        if (userRoll > computerRoll)
        {
            Console.WriteLine($"You win ({userRoll} > {computerRoll})!");
        }
        else if (userRoll < computerRoll)
        {
            Console.WriteLine($"I win ({computerRoll} > {userRoll})!");
        }
        else
        {
            Console.WriteLine("It's a tie!");
        }
    }

    private int SelectDice(string player, int? excludeIndex = null)
    {
        Console.WriteLine("Choose your dice:");
        List<int> availableIndices = new List<int>();

        for (int i = 0; i < dice.Count; i++)
        {
            if (i != excludeIndex)
            {
                availableIndices.Add(i);
                Console.WriteLine($"{availableIndices.Count - 1} - {string.Join(",", dice[i].Faces)}");
            }
        }
        Console.WriteLine("X - exit");
        Console.WriteLine("? - help");
        Console.Write("Your selection: ");

        string? input;
        int choice;
        while (true)
        {
            input = Console.ReadLine();
            if (input == null || input.ToUpper() == "X")
            {
                Environment.Exit(0);
            }
            else if (input == "?")
            {
                ProbabilityTable.DisplayProbabilityTable(dice);
                continue;
            }
            else if (int.TryParse(input, out choice) && choice >= 0 && choice < availableIndices.Count)
            {
                break;
            }
            else
            {
                Console.WriteLine("Invalid choice. Try again.");
            }
        }

        return availableIndices[choice];
    }

    private int SelectRandomDice(int? excludeIndex = null)
    {
        int choice;
        do
        {
            choice = random.Next(dice.Count);
        } while (choice == excludeIndex);

        return choice;
    }

    private int FairRollDice(Dice dice, string player)
    {
        var (computerNumber, key, hmac) = FairRandomGenerator.GenerateFairRandomNumber(dice.Faces.Length);
        Console.WriteLine($"I selected a random value in the range 0..{dice.Faces.Length - 1} (HMAC={BitConverter.ToString(hmac).Replace("-", "")}).");

        Console.WriteLine("Add your number modulo " + dice.Faces.Length + ".");
        for (int i = 0; i < dice.Faces.Length; i++)
        {
            Console.WriteLine($"{i} - {i}");
        }
        Console.WriteLine("X - exit");
        Console.WriteLine("? - help");

        string? input;
        int userNumber;
        while (true)
        {
            input = Console.ReadLine();
            if (input == null || input.ToUpper() == "X")
            {
                Environment.Exit(0);
            }
            else if (input == "?")
            {
                ProbabilityTable.DisplayProbabilityTable(new List<Dice> { dice });
                continue;
            }
            else if (int.TryParse(input, out userNumber) && userNumber >= 0 && userNumber < dice.Faces.Length)
            {
                break;
            }
            else
            {
                Console.WriteLine("Invalid choice. Try again.");
            }
        }

        int result = (userNumber + computerNumber) % dice.Faces.Length;
        Console.WriteLine($"My number is {computerNumber} (KEY={BitConverter.ToString(key).Replace("-", "")}).");
        Console.WriteLine($"The result is {userNumber} + {computerNumber} = {result} (mod {dice.Faces.Length}).");

        return dice.Faces[result];
    }

}