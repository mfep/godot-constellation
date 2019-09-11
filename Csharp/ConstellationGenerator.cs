using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public class ConstellationGenerator : Node2D
{
    [Export]
    public float MinStarDistance = 20.0f;
    [Export]
    public int MinStarsInConstellation = 5;
    [Export]
    public int MaxStarsInConstellation = 10;
    [Export]
    public float MinConstellationRadius = 80.0f;
    [Export]
    public float MaxConstellationRadius = 160.0f;
    [Export]
    public int MinConstellationsInGalaxy = 5;
    [Export]
    public int MaxConstellationsInGalaxy = 10;
    [Export]
    public float GalaxyRadius = 300.0f;
    [Export]
    public float ConstellationConnectionThreshold = 300.0f;

    const int MaxIterations = 1000;

    List<Constellation> constellations = new List<Constellation>();
    List<ConstellationConnection> constellationConnections = new List<ConstellationConnection>();

    struct Connection : IComparable<Connection>
    {
        public int I1;
        public int I2;
        public float Distance;
        public Color Color;

        public int CompareTo(Connection other)
        {
            return Distance.CompareTo(other.Distance);
        }
    }

    class Constellation
    {
        public List<Vector2> StarPositions = new List<Vector2>();
        public List<Connection> Connections = new List<Connection>();
        public Vector2 Center;
        public float Radius;
    }

    struct ConstellationConnection
    {
        public Constellation C1;
        public Constellation C2;
        public int I1;
        public int I2;
    }

    public override void _Ready()
    {
        GenerateGalaxy();
    }

    public override void _UnhandledKeyInput(InputEventKey @event)
    {
        if (Input.IsKeyPressed((int)KeyList.Space))
        {
            GenerateGalaxy();
        }
    }

    public override void _Draw()
    {
        foreach (var constellation in constellations)
        {
            foreach (var star in constellation.StarPositions)
            {
                DrawCircle(star, 3.0f, Color.ColorN("white"));
            }
            foreach (var connection in constellation.Connections)
            {
                var pos1 = constellation.StarPositions[connection.I1];
                var pos2 = constellation.StarPositions[connection.I2];
                DrawLine(pos1, pos2, connection.Color, 1, true);
            }
        }
        foreach (var connection in constellationConnections)
        {
            var pos1 = connection.C1.StarPositions[connection.I1];
            var pos2 = connection.C2.StarPositions[connection.I2];
            DrawLine(pos1, pos2, Color.ColorN("red", 0.5f));
        }
    }

    void GenerateGalaxy()
    {
        var startMs = OS.GetTicksUsec() / 1000.0f;
        constellations = GenerateConstellations(MinConstellationsInGalaxy, MaxConstellationsInGalaxy);
        constellationConnections = ConnectNearestConstellations(constellations);
        ConnectAdditionalConstellations(constellations, constellationConnections, ConstellationConnectionThreshold);
        
        GD.Print($"Regeneration took {OS.GetTicksUsec() / 1000.0f - startMs} ms");
        Update();
    }

    static int RandomRange(int minVal, int maxVal)
    {
        var result = ((int)(GD.Randi() / 2) % (maxVal - minVal + 1)) + minVal;
        return result;
    }

    List<Constellation> GenerateConstellations(int minNumberOfConstellations, int maxNumberOfConstellations)
    {
        var constellations = new List<Constellation>();
        var numberOfConstellations = RandomRange(minNumberOfConstellations, maxNumberOfConstellations);
        constellations = new List<Constellation>();
        var counter = 0;
        while (constellations.Count < numberOfConstellations)
        {
            var r = (float)GD.RandRange(0.0f, GalaxyRadius);
            var phi = (float)GD.RandRange(0.0f, 2 * Mathf.Pi);
            var centerCandidate = new Vector2(r * Mathf.Cos(phi), r * Mathf.Sin(phi));
            if (constellations.Any(constellation => (constellation.Center - centerCandidate).Length() < constellation.Radius))
            {
                if (++counter > MaxIterations)
                {
                    break;
                }
            }
            var constellationCandidate = GenerateConstellation(centerCandidate);
            if (constellations.All(constellation => (constellation.Center - centerCandidate).Length() > constellation.Radius + constellationCandidate.Radius))
            {
                constellations.Add(constellationCandidate);
            }
            else
            {
                if (++counter > MaxIterations)
                {
                    break;
                }
            }
        }
        return constellations;
    }

    static List<ConstellationConnection> ConnectNearestConstellations(List<Constellation> constellations)
    {
        var constellationConnections = new List<ConstellationConnection>();
        var connectedConstellations = new HashSet<Constellation> { constellations[0] };
        while (connectedConstellations.Count < constellations.Count)
        {
            var minDistance = float.MaxValue;
            Constellation constellationToConnect = null;
            Constellation newConnectedConstellation = null;
            foreach (var connectedConstellation in connectedConstellations)
            {
                foreach (var notConnectedConstellation in constellations.Where(constellation => !connectedConstellations.Contains(constellation)))
                {
                    var centerDistance = (connectedConstellation.Center - notConnectedConstellation.Center).Length();
                    if (centerDistance < minDistance)
                    {
                        minDistance = centerDistance;
                        constellationToConnect = connectedConstellation;
                        newConnectedConstellation = notConnectedConstellation;
                    }
                }
            }
            connectedConstellations.Add(newConnectedConstellation);
            constellationConnections.Add(GenerateConstellationConnection(constellationToConnect, newConnectedConstellation));
        }
        return constellationConnections;
    }

    static void ConnectAdditionalConstellations(List<Constellation> constellations, List<ConstellationConnection> connections, float distanceThreshold)
    {
        foreach (var constellationA in constellations)
        {
            foreach (var constellationB in constellations)
            {
                if (constellationA == constellationB || AreConstellationsDirectlyConnected(connections, constellationA, constellationB))
                {
                    continue;
                }
                var centerDistance = (constellationA.Center - constellationB.Center).Length();
                if (GD.Randf() > centerDistance / distanceThreshold)
                {
                    connections.Add(GenerateConstellationConnection(constellationA, constellationB));
                }
            }
        }
    }

    static bool AreConstellationsDirectlyConnected(List<ConstellationConnection> connections, Constellation constellationA, Constellation constellationB) =>
        connections.Any(conn => (conn.C1 == constellationA && conn.C2 == constellationB) || (conn.C1 == constellationB && conn.C2 == constellationA));

    static ConstellationConnection GenerateConstellationConnection(Constellation constellationA, Constellation constellationB)
    {
        float minStarDistance = float.MaxValue;
        int selectedStarA = -1;
        int selectedStarB = -1;
        for (int i = 0; i < constellationA.StarPositions.Count; i++)
        {
            for (int j = 0; j < constellationB.StarPositions.Count; j++)
            {
                var starDistance = (constellationA.StarPositions[i] - constellationB.StarPositions[j]).Length();
                if (starDistance < minStarDistance)
                {
                    selectedStarA = i;
                    selectedStarB = j;
                    minStarDistance = starDistance;
                }
            }
        }
        return new ConstellationConnection { C1 = constellationA, C2 = constellationB, I1 = selectedStarA, I2 = selectedStarB };
    }

    static IEnumerable<System.Tuple<Vector2, Vector2>> EnumerateAllEdges(List<Constellation> constellations, List<ConstellationConnection> connections)
    {
        foreach (var constellation in constellations)
        {
            foreach (var connection in constellation.Connections)
            {
                yield return new Tuple<Vector2, Vector2>(constellation.StarPositions[connection.I1], constellation.StarPositions[connection.I2]);
            }
        }
        foreach (var connection in connections)
        {
            var c1 = connection.C1;
            var c2 = connection.C2;
            yield return new Tuple<Vector2, Vector2>(c1.StarPositions[connection.I1], c2.StarPositions[connection.I2]);
        }
    }

    Constellation GenerateConstellation(Vector2 center)
    {
        float actualRadius;
        var maxRadius = (float)GD.RandRange(MinConstellationRadius, MaxConstellationRadius);
        var starPositions = GenerateStarPositions(center, MinStarsInConstellation, MaxStarsInConstellation, maxRadius, MinStarDistance, out actualRadius);
        var connections = NearestStarConnections(starPositions);

        var connectionGroups = FindConnected(connections);
        var connectedGroupIndices = new HashSet<int>();
        for (int i = 0; i < connectionGroups.Count; i++)
        {
            for (int j = 0; j < connectionGroups.Count; j++)
            {
                if (i == j || connectedGroupIndices.Contains(i) && connectedGroupIndices.Contains(j))
                {
                    continue;
                }
                if (ConnectGroups(starPositions, connections, connectionGroups[i], connectionGroups[j]))
                {
                    connectedGroupIndices.Add(i);
                    connectedGroupIndices.Add(j);
                }
            }
        }
        return new Constellation { StarPositions = starPositions, Connections = connections, Center = center, Radius = actualRadius };
    }

    static List<Vector2> GenerateStarPositions(Vector2 center, int minStars, int maxStars, float maxRadius, float minDistance, out float actualRadius)
    {
        actualRadius = 0.0f;
        var starCount = RandomRange(minStars, maxStars);
        var starPositions = new List<Vector2>();
        var counter = 0;
        while (starPositions.Count < starCount)
        {
            var radius = (float)GD.RandRange(0.0f, maxRadius);
            var angle = (float)GD.RandRange(0.0f, Mathf.Pi * 2.0f);
            var starCandidate = new Vector2(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle)) + center;
            if (starPositions.All(pos => (starCandidate - pos).Length() >= minDistance))
            {
                actualRadius = Mathf.Max(actualRadius, radius);
                starPositions.Add(starCandidate);
            }
            if (++counter == MaxIterations)
            {
                break;
            }
        }
        return starPositions;
    }

    static List<Connection> NearestStarConnections(List<Vector2> starPositions)
    {
        List<Connection> connections = new List<Connection>();
        HashSet<int> connectedIndices = new HashSet<int>();
        for (int masterIndex = 0; masterIndex < starPositions.Count; masterIndex++)
        {
            if (connectedIndices.Contains(masterIndex)) continue;

            var selectedDistance = float.PositiveInfinity;
            var selectedIndex = -1;
            for (int slaveIndex = 0; slaveIndex < starPositions.Count; slaveIndex++)
            {
                if (masterIndex == slaveIndex) continue;
                var distance = (starPositions[masterIndex] - starPositions[slaveIndex]).Length();
                if (distance < selectedDistance)
                {
                    selectedDistance = distance;
                    selectedIndex = slaveIndex;
                }
            }
            connectedIndices.Add(masterIndex);
            connectedIndices.Add(selectedIndex);
            connections.Add(new Connection { I1 = masterIndex, I2 = selectedIndex, Distance = selectedDistance, Color = new Color(1, 1, 1, 0.5f) });
        }
        return connections;
    }

    static List<HashSet<int>> FindConnected(List<Connection> connections)
    {
        var connectedSets = new List<HashSet<int>>();
        var appearedIndices = new HashSet<int>();
        foreach (var connection in connections)
        {
            if (appearedIndices.Contains(connection.I1) || appearedIndices.Contains(connection.I2))
            {
                foreach (var set in connectedSets)
                {
                    if (set.Contains(connection.I1))
                    {
                        set.Add(connection.I2);
                        break;
                    }
                    if (set.Contains(connection.I2))
                    {
                        set.Add(connection.I1);
                        break;
                    }
                }
                appearedIndices.Add(connection.I1);
                appearedIndices.Add(connection.I2);
            }
            else
            {
                connectedSets.Add(new HashSet<int> { connection.I1, connection.I2 });
                appearedIndices.Add(connection.I1);
                appearedIndices.Add(connection.I2);
            }
        }
        return connectedSets;
    }

    static bool ConnectionIntersects(List<Vector2> positions, List<Connection> connections, Connection potentialConnection)
    {
        var pos1 = positions[potentialConnection.I1];
        var pos2 = positions[potentialConnection.I2];
        return connections
            .Where(connection => connection.I1 != potentialConnection.I1
                && connection.I2 != potentialConnection.I2
                && connection.I1 != potentialConnection.I2
                && connection.I2 != potentialConnection.I1)
            .Any(connection => Intersection.doIntersect(positions[connection.I1], positions[connection.I2], pos1, pos2));
    }

    static bool ConnectGroups(List<Vector2> positions, List<Connection> connections, HashSet<int> groupA, HashSet<int> groupB)
    {
        var potentialConnections = new List<Connection>();
        foreach (var masterIndex in groupA)
        {
            foreach (var slaveIndex in groupB)
            {
                var distance = (positions[masterIndex] - positions[slaveIndex]).Length();
                potentialConnections.Add(new Connection { I1 = masterIndex, I2 = slaveIndex, Distance = distance, Color = new Color(0, 1, 1, 0.5f) });
            }
        }
        potentialConnections.Sort();
        foreach (var potentialConnection in potentialConnections)
        {
            if (!ConnectionIntersects(positions, connections, potentialConnection))
            {
                connections.Add(potentialConnection);
                return true;
            }
        }
        return false;
    }
}
