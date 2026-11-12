using Godot;

namespace DeadFrequency;

public partial class CityGenerator
{
    private void BuildBikeLanes()
    {
        if (_useLogicalBlockPlans)
            return; // As faixas acompanham os segmentos de chunk correspondentes.

        const float laneWidth = 1.1f;
        const float intersectionClearance = 2f;
        StandardMaterial3D lane = Flat(new Color(0.13f, 0.3f, 0.18f), 0.95f);
        StandardMaterial3D divider = Flat(new Color(0.72f, 0.75f, 0.67f), 0.9f);
        int ring = Mathf.Clamp(Radius - 1, 1, 3);

        foreach (int road in new[] { -ring - 1, ring })
        {
            float roadPos = road * Period + Period * 0.5f;
            float side = road < 0 ? 1f : -1f;
            for (int segment = -ring; segment <= ring; segment++)
                BuildBikeLaneSegment(roadPos, side, segment, laneWidth, intersectionClearance, lane, divider, _staticRoot);
        }
    }

    private void BuildBikeLanesForBlock(int bx, int bz)
    {
        const float laneWidth = 1.1f;
        const float intersectionClearance = 2f;
        StandardMaterial3D lane = Flat(new Color(0.13f, 0.3f, 0.18f), 0.95f);
        StandardMaterial3D divider = Flat(new Color(0.72f, 0.75f, 0.67f), 0.9f);
        int ring = Mathf.Clamp(Radius - 1, 1, 3);
        Node3D parent = _activeChunkRoot ?? _staticRoot;

        foreach (int road in new[] { -ring - 1, ring })
        {
            float roadPos = road * Period + Period * 0.5f;
            float side = road < 0 ? 1f : -1f;
            int verticalOwnerX = side > 0f ? road + 1 : road;
            int horizontalOwnerZ = side > 0f ? road + 1 : road;

            if (bx == verticalOwnerX && bz >= -ring && bz <= ring)
                BuildBikeLaneSegment(roadPos, side, bz, laneWidth, intersectionClearance, lane, divider, parent);
            if (bz == horizontalOwnerZ && bx >= -ring && bx <= ring)
                BuildBikeLaneSegment(roadPos, side, bx, laneWidth, intersectionClearance, lane, divider, parent);
        }
    }

    private void BuildBikeLaneSegment(
        float roadPos,
        float side,
        int segment,
        float laneWidth,
        float intersectionClearance,
        Material lane,
        Material divider,
        Node3D parent)
    {
        float along = segment * Period;
        float length = BlockSize - intersectionClearance * 2f;
        float lanePos = roadPos + side * (RoadWidth * 0.5f - laneWidth * 0.5f);
        AddBox(parent, new Vector3(lanePos, 0.012f, along), new Vector3(laneWidth, 0.02f, length), lane, false);
        float dividerPos = roadPos + side * (RoadWidth * 0.5f - laneWidth);
        AddBox(parent, new Vector3(dividerPos, 0.025f, along), new Vector3(0.07f, 0.015f, length), divider, false);
        AddBox(parent, new Vector3(along, 0.012f, lanePos), new Vector3(length, 0.02f, laneWidth), lane, false);
        AddBox(parent, new Vector3(along, 0.025f, dividerPos), new Vector3(length, 0.015f, 0.07f), divider, false);
    }
}
