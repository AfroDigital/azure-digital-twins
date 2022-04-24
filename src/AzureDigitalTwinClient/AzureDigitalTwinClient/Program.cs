using Azure;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Microsoft.Azure.DigitalTwins.Parser;
using Newtonsoft.Json;

public partial class Program
{
    private static readonly string adtInstanceUrl = "<enter-adt-instance>";
    private static readonly DefaultAzureCredential credential = new DefaultAzureCredential();
    private static readonly DigitalTwinsClient client = new(new Uri(adtInstanceUrl), credential);
    private static readonly string regionName = "ohiovalley";

    private static readonly Dictionary<string, string> modelRepository = new Dictionary<string, string> {
        { "facility", "Models/facility.json" },
        { "power-vacuum", "Models/power-vacuum.json" },
        { "room", "Models/room-2.json" },
        { "sterilizer", "Models/sterilizer.json" },
        { "region", "Models/region.json" }
    };


    static async Task Main(string[] args)
    {

        Console.WriteLine($"Azure Service client created – ready to go");

        Console.WriteLine();

        // Upload All Models
        //await UploadAllModels();
        //Console.WriteLine($"Uploading models");

        //await CreateSalesRegion(regionName);
        //

        await CreateDentalFacility("wifiteeth", regionName, 4);

        //JsonPatchDocument updateDoc = new JsonPatchDocument();

        //updateDoc.AppendReplace("/facilityGeoCoordinates/latitude", 98.9);
        //updateDoc.AppendReplace("/facilityGeoCoordinates/longitude", 45.6);

        //await UpdateDigitalTwin("grandlakesdental", updateDoc);



        Console.ReadLine();


    }

    private static async Task CreateSalesRegion(string name)
    {
        var regionModel = modelRepository.Where(s => s.Key == "region").Select(s => s.Value).FirstOrDefault();

        await CreateDeviceTwin(regionModel, name);

    }
    private async static Task CreateDentalFacility(string facilityName, string regionName, int numberofRooms)
    {
        var facilityModel = modelRepository.Where(s => s.Key == "facility").Select(s => s.Value).FirstOrDefault();
        var roomModel = modelRepository.Where(s => s.Key == "room").Select(s => s.Value).FirstOrDefault();
        var powerVacuumModel = modelRepository.Where(s => s.Key == "power-vacuum").Select(s => s.Value).FirstOrDefault();
        var sterilizerModel = modelRepository.Where(s => s.Key == "sterilizer").Select(s => s.Value).FirstOrDefault();


        await CreateDeviceTwin(facilityModel, facilityName);

        //create region to facility relationship
        await CreateRelationshipAsync(facilityName, regionName, "is_located_in");

        for (int i = 0; i < numberofRooms; i++)
        {
            var roomName = $"{facilityName}room{i + 1}";
            var vacuumName = roomName + "vacuum";
            var sterilizerName = roomName + "sterilizer";

            await CreateDeviceTwin(roomModel, roomName);

            await CreateDeviceTwin(powerVacuumModel, vacuumName);

            await CreateDeviceTwin(sterilizerModel, sterilizerName);

            //create relationships

            //create facility to room relationship

            await CreateRelationshipAsync(facilityName, roomName, "has_room");

            //create room to vacuum relationship
            await CreateRelationshipAsync(roomName, vacuumName, "has_power_vacuum");

            //create room to sterilizer relationship
            await CreateRelationshipAsync(roomName, sterilizerName, "has_sterilizer");
        }



    }


    private static async Task UploadAllModels()
    {
        foreach (var model in modelRepository)
        {
            await UploadModel(model.Value);
        }
    }

    private async Task<BasicDigitalTwin> GetDigitaTwin(string twinId)
    {
        CancellationToken cancellationToken = new CancellationToken();
        BasicDigitalTwin digitalTwin = new BasicDigitalTwin();

        try
        {

            Response<BasicDigitalTwin> digitalTwinResponse = await client.GetDigitalTwinAsync<BasicDigitalTwin>(twinId, cancellationToken);
            digitalTwin = digitalTwinResponse.Value;

        }
        catch (RequestFailedException e)
        {
            Console.WriteLine($"Create twin error: {e.Status}: {e.Message}");
        }

        return digitalTwin;
    }

    private static async Task UpdateDigitalTwin(string twinId, JsonPatchDocument updateDoc)
    {
        CancellationToken cancellationToken = new CancellationToken();


        try
        {

            await client.UpdateDigitalTwinAsync(twinId, updateDoc, null, cancellationToken);
            Console.WriteLine($"twin updated");


        }
        catch (RequestFailedException e)
        {
            Console.WriteLine($"Update twin error: {e.Status}: {e.Message}");
        }

    }



    private static async Task CreateDeviceTwin(string modelFilePath, string facilityName)
    {
        var model = LoadModel(modelFilePath);
        var modelInterface = await LoadModelInterface(model);
        await CreateTwin(facilityName, modelInterface.Id.ToString());
    }


    private static async Task UploadModel(string modelDtdlFilePath)
    {
        var model = LoadModel(modelDtdlFilePath);
        var currentInterface = await LoadModelInterface(model);

        if (currentInterface != null)
        {
            var modelExists = await ModelExists(currentInterface.Id.ToString());

            if (!modelExists)
            {
                try
                {
                    await client.CreateModelsAsync(model);
                    Console.WriteLine($"Model {currentInterface.DisplayName.Values.FirstOrDefault()} uploaded to the digital twin:");
                }
                catch (RequestFailedException e)
                {
                    Console.WriteLine($"Upload model error: {e.Status}: {e.Message}");
                }
            }
        }



    }


    private static async Task<DTInterfaceInfo> LoadModelInterface(List<string> model)
    {

        var parser = new ModelParser();
        IReadOnlyDictionary<Dtmi, DTEntityInfo> deviceTwinObjectModel = await parser.ParseAsync(model);


        var interfaces = new List<DTInterfaceInfo>();
        IEnumerable<DTInterfaceInfo> ifenum =
            from entity in deviceTwinObjectModel.Values
            where entity.EntityKind == DTEntityKind.Interface
            select entity as DTInterfaceInfo;
        interfaces.AddRange(ifenum);

        var currentInterface = interfaces.FirstOrDefault();

        return currentInterface;

    }


    private static List<string> LoadModel(string modelPath)
    {
        string deviceTwinModel = File.ReadAllText(modelPath);
        return new List<string> { deviceTwinModel };
    }

    private static async Task<bool> ModelExists(string modelId)
    {
        var exists = false;
        AsyncPageable<DigitalTwinsModelData> modelDataList = client.GetModelsAsync();
        await foreach (DigitalTwinsModelData md in modelDataList)
        {
            exists = modelId == md.Id;
        }
        return exists;
    }

    private static async Task CreateTwin(string twinName, string modelId)
    {
        var twinData = new BasicDigitalTwin();
        twinData.Metadata.ModelId = modelId;

        try
        {
            twinData.Id = $"{twinName}";
            await client.CreateOrReplaceDigitalTwinAsync<BasicDigitalTwin>(twinData.Id, twinData);
            Console.WriteLine($"Created twin: {twinData.Id}");
        }
        catch (RequestFailedException e)
        {
            Console.WriteLine($"Create twin error: {e.Status}: {e.Message}");
        }

    }

    private async static Task CreateRelationshipAsync(string srcId, string targetId, string relationshipName)
    {
        var relationship = new BasicRelationship
        {
            TargetId = targetId,
            Name = relationshipName
        };

        try
        {
            string relId = $"{srcId}-{relationshipName}->{targetId}";
            await client.CreateOrReplaceRelationshipAsync(srcId, relId, relationship);
            Console.WriteLine("Created relationship successfully");
        }
        catch (RequestFailedException e)
        {
            Console.WriteLine($"Create relationship error: {e.Status}: {e.Message}");
        }
    }

    private async static Task ListRelationshipsAsync(string srcId)
    {
        try
        {
            AsyncPageable<BasicRelationship> results = client.GetRelationshipsAsync<BasicRelationship>(srcId);
            Console.WriteLine($"Twin {srcId} is connected to:");
            await foreach (BasicRelationship rel in results)
            {
                Console.WriteLine($" -{rel.Name}->{rel.TargetId}");
            }
        }
        catch (RequestFailedException e)
        {
            Console.WriteLine($"Relationship retrieval error: {e.Status}: {e.Message}");
        }
    }

}
