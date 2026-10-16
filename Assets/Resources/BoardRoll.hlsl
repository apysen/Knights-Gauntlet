#ifndef KG_BOARD_ROLL_INCLUDED
#define KG_BOARD_ROLL_INCLUDED
// x: flat-to-curve join, y: radius, z: end of return, w: enabled.
float4 _KGBoardRoll;
float3 KGRollPosition(float3 p)
{
    if (_KGBoardRoll.w < .5 || p.y <= _KGBoardRoll.x) return p;
    float angle = (p.y - _KGBoardRoll.x) / _KGBoardRoll.y;
    float radius = _KGBoardRoll.y - p.z;
    return float3(p.x, _KGBoardRoll.x + radius * sin(angle), _KGBoardRoll.y - radius * cos(angle));
}
float3 KGRollNormal(float3 normal, float originalY)
{
    float angle = max(0, originalY - _KGBoardRoll.x) / max(.01, _KGBoardRoll.y) * _KGBoardRoll.w;
    float s = sin(angle), c = cos(angle);
    return float3(normal.x, c * normal.y - s * normal.z, s * normal.y + c * normal.z);
}
#endif
