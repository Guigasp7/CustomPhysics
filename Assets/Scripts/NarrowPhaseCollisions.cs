using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct CollisionPoint
{
    public GYTCollider firstCollider;
    public GYTCollider secondCollider;
    public Vector3 normal;
    public float penetrationDepth;
    public Vector3 contactPoint;
}

public class Simplex
{
    private List<Vector3> points = new List<Vector3>();
    private List<Tuple<List<Vector3>, List<Vector3>>> supportPoints = new List<Tuple<List<Vector3>, List<Vector3>>>();
    public List<Vector3> Points { get { return points; } }
    public List<Tuple<List<Vector3>, List<Vector3>>> SupportPoints { get { return supportPoints; } }

    public void AddPoint(Vector3 _point, List<Vector3> _supportPoints1, List<Vector3> _supportPoints2)
    {
        points.Add(_point);
        supportPoints.Add(Tuple.Create(_supportPoints1, _supportPoints2));
    }

    public void RemovePoint(Vector3 _point)
    {
        int index = points.IndexOf(_point);
        points.RemoveAt(index);
        supportPoints.RemoveAt(index);
    }

    public void RemovePoint(int _index)
    {
        points.RemoveAt(_index);
        supportPoints.RemoveAt(_index);
    }
}

public class NarrowPhaseCollisions : MonoBehaviour
{
    [SerializeField][Range(1, 100)] int maxIterations = 30;
    [SerializeField][Range(0.0001f, 0.1f)] float tolerance = 0.0001f;

    #region Unity Methods
    public List<CollisionPoint> UpdateCollisions(List<Tuple<GYTCollider, GYTCollider>> _collisionPairs)
    {
        List<CollisionPoint> collisionPoints = new List<CollisionPoint>();
        for (int i = 0; i < _collisionPairs.Count; ++i)
        {
            Tuple<bool, Simplex> result = GJK(_collisionPairs[i].Item1, _collisionPairs[i].Item2);
            if (result.Item1)
            {
                if (_collisionPairs[i].Item1.IsTrigger || _collisionPairs[i].Item2.IsTrigger)
                {
                    _collisionPairs[i].Item1.Trigger(_collisionPairs[i].Item2);
                    _collisionPairs[i].Item2.Trigger(_collisionPairs[i].Item1);
                    continue;
                }

                // If the two are dynamic an one is sleeping, wake it up
                if(_collisionPairs[i].Item1.Rigidbody != null && _collisionPairs[i].Item2.Rigidbody != null) 
                {
                    if(_collisionPairs[i].Item1.Rigidbody.isSleeping)
                        _collisionPairs[i].Item1.Rigidbody.WakeUp();

                    else if (_collisionPairs[i].Item2.Rigidbody.isSleeping)
                        _collisionPairs[i].Item2.Rigidbody.WakeUp();
                }

                CollisionPoint point = EPA(result.Item2, _collisionPairs[i].Item1, _collisionPairs[i].Item2);
                collisionPoints.Add(point);
            }
        }
        return collisionPoints;
    }

    #endregion

    #region GJK Methods

    private Tuple<bool, Simplex> GJK(GYTCollider _collider1, GYTCollider _collider2)
    {
        Simplex simplex = new Simplex();
        Vector3 _direction = new Vector3(1f, 1f, 1f);
        // First Point
        List<Vector3> points1 = _collider1.SupportFunction(_direction);
        List<Vector3> points2 = _collider2.SupportFunction(-_direction);
        simplex.AddPoint(points1[0] - points2[0], points1, points2);
        _direction = -_direction;

        // Loop
        while (true)
        {
            List<Vector3> pointsA = _collider1.SupportFunction(_direction);
            List<Vector3> pointsB = _collider2.SupportFunction(-_direction);
            Vector3 nextPoint = pointsA[0] - pointsB[0];
            if (Vector3.Dot(nextPoint, _direction) < 0f)
                return new Tuple<bool, Simplex>(false, simplex);
            else
                simplex.AddPoint(nextPoint, pointsA, pointsB);

            if (CreateSimplex(ref simplex, ref _direction))
                return new Tuple<bool, Simplex>(true, simplex);
        }
    }

    private bool CreateSimplex(ref Simplex _simplex, ref Vector3 _direction)
    {
        switch (_simplex.Points.Count)
        {
            case 1:
                // Second Point
                _direction = -_simplex.Points[0];
                return false;
            case 2:
                // Third Point
                Vector3 AB = _simplex.Points[0] - _simplex.Points[1];
                Vector3 thirdVector = Vector3.zero;
                if (Vector3.Dot(AB, -_simplex.Points[1]) >= 0f)
                {
                    thirdVector = Vector3.Cross(Vector3.Cross(AB, -_simplex.Points[1]), AB);
                }
                else
                {
                    thirdVector = -_simplex.Points[1];
                }
                // If the two colliders are perfectly clipped, select a random vector that is not the base direction
                if (thirdVector.magnitude < 0.0001f)
                {
                    while (Mathf.Abs(thirdVector.x - thirdVector.y) < 0.0001f && Mathf.Abs(thirdVector.x - thirdVector.z) < 0.0001f)
                        thirdVector = UnityEngine.Random.onUnitSphere;
                }
                _direction = thirdVector;
                return false;
            case 3:
                // Fourth Point
                Vector3 fourthVector = Vector3.zero;
                AB = _simplex.Points[1] - _simplex.Points[2];
                Vector3 AC = _simplex.Points[0] - _simplex.Points[2];
                Vector3 ABC = Vector3.Cross(AB, AC);
                //// AC Region
                Vector3 cross1 = Vector3.Cross(ABC, AC);
                Vector3 cross2 = Vector3.Cross(AB, ABC);
                if (Vector3.Dot(cross1, -_simplex.Points[2]) >= 0f)
                {
                    if (Vector3.Dot(AC, -_simplex.Points[2]) >= 0f)
                    {
                        fourthVector = Vector3.Cross(Vector3.Cross(AC, -_simplex.Points[2]), AC);
                    }
                    else
                    {
                        // Star check
                        if (Vector3.Dot(AB, -_simplex.Points[2]) >= 0f)
                        {
                            fourthVector = Vector3.Cross(Vector3.Cross(AB, -_simplex.Points[2]), AB);
                        }
                        else
                        {
                            fourthVector = -_simplex.Points[2];
                        }
                    }
                }
                else
                {
                    // AB Region
                    if (Vector3.Dot(cross2, -_simplex.Points[2]) >= 0f)
                    {
                        // Star check
                        if (Vector3.Dot(AB, -_simplex.Points[2]) >= 0f)
                        {
                            fourthVector = Vector3.Cross(Vector3.Cross(AB, -_simplex.Points[2]), AB);
                        }
                        else
                        {
                            fourthVector = -_simplex.Points[2];
                        }
                    }
                    else
                    {
                        if (Vector3.Dot(ABC, -_simplex.Points[2]) >= 0f)
                        {
                            fourthVector = ABC;
                        }
                        else
                        {
                            fourthVector = -ABC;
                        }
                    }
                }
                _direction = fourthVector;
                return false;
            case 4:
                return CheckCollision(ref _simplex, ref _direction);
            default:
                return false;
        }
    }

    private bool CheckCollision(ref Simplex _simplex, ref Vector3 _direction)
    {
        Vector3 segment1 = _simplex.Points[1] - _simplex.Points[0]; //AB
        Vector3 segment2 = _simplex.Points[2] - _simplex.Points[0]; //AC
        Vector3 segment3 = _simplex.Points[3] - _simplex.Points[0]; //AD
        Vector3 segment4 = _simplex.Points[2] - _simplex.Points[1]; //BC
        Vector3 segment5 = _simplex.Points[3] - _simplex.Points[1]; //BD

        Vector3 tetrahedronCenter = (_simplex.Points[0] + _simplex.Points[1] + _simplex.Points[2] + _simplex.Points[3]) / 4f;

        // ABC
        Vector3 center = (_simplex.Points[0] + _simplex.Points[1] + _simplex.Points[2]) / 3f;
        Vector3 face1 = Vector3.Cross(segment1, segment2);
        if (Vector3.Dot(face1, tetrahedronCenter - center) < 0)
        {
            face1 = -face1;
        }
        if (Vector3.Dot(face1, -_simplex.Points[0]) < 0f)
        {
            // Remove D
            _simplex.RemovePoint(3);
            _direction = -face1;
            return false;
        }

        // ACD
        center = (_simplex.Points[0] + _simplex.Points[3] + _simplex.Points[2]) / 3f;
        Vector3 face2 = Vector3.Cross(segment2, segment3);
        if (Vector3.Dot(face2, tetrahedronCenter - center) < 0)
        {
            face2 = -face2;
        }
        if (Vector3.Dot(face2, -_simplex.Points[0]) < 0f)
        {
            // Remove B
            _simplex.RemovePoint(1);
            _direction = -face2;
            return false;
        }

        // ABD
        center = (_simplex.Points[0] + _simplex.Points[1] + _simplex.Points[3]) / 3f;
        Vector3 face3 = Vector3.Cross(segment3, segment1);
        if (Vector3.Dot(face3, tetrahedronCenter - center) < 0)
        {
            face3 = -face3;
        }
        if (Vector3.Dot(face3, -_simplex.Points[0]) < 0f)
        {
            // Remove C
            _simplex.RemovePoint(2);
            _direction = -face3;
            return false;
        }

        // BCD
        center = (_simplex.Points[1] + _simplex.Points[2] + _simplex.Points[3]) / 3f;
        Vector3 face4 = Vector3.Cross(segment5, segment4);
        if (Vector3.Dot(face4, tetrahedronCenter - center) < 0)
        {
            face4 = -face4;
        }
        if (Vector3.Dot(face4, -_simplex.Points[1]) < 0f)
        {
            // Remove A
            _simplex.RemovePoint(0);
            _direction = -face4;
            return false;
        }
        return true;
    }

    #endregion

    #region EPA Methods
    private CollisionPoint EPA(Simplex _simplex, GYTCollider _collider1, GYTCollider _collider2)
    {
        int iterations = 0;
        List<int> faces = new List<int>()
        {
            0, 1, 2,
            0, 3, 1,
            0, 2, 3,
            1, 3, 2
        };
        List<Vector4> normals = new List<Vector4>();

        // STEP 1: Get Closest face
        int minIndex = GetClosestFace(_simplex.Points, faces, ref normals, tolerance);
        Vector4 previousNormal = normals[minIndex];
        List<int> previousFaces = new(faces);

        Vector3 minNormal = Vector3.zero;
        float minDistance = float.MaxValue;

        // Loop
        while (minDistance == float.MaxValue)
        {
            minNormal = normals[minIndex];
            minDistance = normals[minIndex].w;

            List<Vector3> points1 = _collider1.SupportFunction(minNormal);
            List<Vector3> points2 = _collider2.SupportFunction(-minNormal);
            Vector3 supportPoint = points1[0] - points2[0];
            float supportDistance = Vector3.Dot(minNormal, supportPoint);

            if (Mathf.Abs(supportDistance - minDistance) > tolerance)
            {
                minDistance = float.MaxValue;
                List<Tuple<int, int>> uniqueEdges = new List<Tuple<int, int>>();

                // STEP 2: Remove all faces pointing towards the support point
                for (int i = 0; i < normals.Count; ++i)
                {
                    // If pointing towards support point
                    if (Vector3.Dot(normals[i], supportPoint - _simplex.Points[faces[i * 3]]) > 0f)
                    {
                        int index = i * 3;

                        AddEdge(ref uniqueEdges, faces, index, index + 1);
                        AddEdge(ref uniqueEdges, faces, index + 1, index + 2);
                        AddEdge(ref uniqueEdges, faces, index + 2, index);

                        faces[index + 2] = faces.Last(); faces.RemoveAt(faces.Count - 1);
                        faces[index + 1] = faces.Last(); faces.RemoveAt(faces.Count - 1);
                        faces[index] = faces.Last(); faces.RemoveAt(faces.Count - 1);

                        normals[i] = normals.Last();
                        normals.RemoveAt(normals.Count - 1);

                        --i;
                    }
                }

                // If no unique edges are found, we reached the end of the polytope
                if (uniqueEdges.Count == 0)
                {
                    faces = previousFaces;
                    minDistance = previousNormal.w;
                    minNormal = previousNormal;

                    break;
                }

                // STEP 3: Create new faces
                List<int> newFaces = new List<int>();
                foreach (Tuple<int, int> edge in uniqueEdges)
                {
                    newFaces.Add(edge.Item1);
                    newFaces.Add(edge.Item2);
                    newFaces.Add(_simplex.Points.Count);
                }

                _simplex.AddPoint(supportPoint, points1, points2);

                // STEP 4: Get new closest face
                List<Vector4> newNormals = new List<Vector4>();
                int newMinIndex = GetClosestFace(_simplex.Points, newFaces, ref newNormals, tolerance);

                float oldMinDistance = float.MaxValue;
                for (int i = 0; i < normals.Count; ++i)
                {
                    if (normals[i].w < oldMinDistance)
                    {
                        oldMinDistance = normals[i].w;
                        minIndex = i;
                    }
                }

                if (newNormals[newMinIndex].w < oldMinDistance)
                {
                    minIndex = newMinIndex + normals.Count;
                }

                faces.AddRange(newFaces);
                normals.AddRange(newNormals);

                previousNormal = normals[minIndex];
                previousFaces = new(faces);

                ++iterations;
                if (iterations == maxIterations)
                {
                    minNormal = normals[minIndex];
                    minDistance = normals[minIndex].w;

                    break;
                }
            }
        }

        CollisionPoint result = new CollisionPoint();
        result.firstCollider = _collider1;
        result.secondCollider = _collider2;
        result.normal = -minNormal;
        result.penetrationDepth = Mathf.Max(minDistance - tolerance - 0.001f, 0f);

        // Contact point
        // Step 1: Project origin onto triangle
        Vector3 center = (_simplex.Points[faces[minIndex * 3]] + _simplex.Points[faces[minIndex * 3 + 1]] + _simplex.Points[faces[minIndex * 3 + 2]]) / 3f;
        float originDistance = Vector3.Dot(center, minNormal);
        Vector3 closestPoint = minNormal * originDistance;

        // Step 2: Get point's barycentric coordinates
        Vector3 A = _simplex.Points[faces[minIndex * 3]];
        Vector3 B = _simplex.Points[faces[minIndex * 3 + 1]];
        Vector3 C = _simplex.Points[faces[minIndex * 3 + 2]];

        float X = 0f;
        float Y = 0f;
        float Z = 0f;

        Vector3 barycentricClosestPoint = Vector3.zero;

        if ((A - closestPoint).magnitude < tolerance)
        {
            barycentricClosestPoint = A;
        }
        else if ((B - closestPoint).magnitude < tolerance)
        {
            barycentricClosestPoint = B;
        }
        else if ((C - closestPoint).magnitude < tolerance)
        {
            barycentricClosestPoint = C;
        }
        else
        {
            float sAreaPBC = GetSignedArea(closestPoint, B, C, minNormal);
            float sAreaAPC = GetSignedArea(A, closestPoint, C, minNormal);
            float sAreaABP = GetSignedArea(A, B, closestPoint, minNormal);
            float sAreaABC = GetSignedArea(A, B, C, minNormal);

            X = sAreaPBC / sAreaABC;
            Y = sAreaAPC / sAreaABC;
            Z = sAreaABP / sAreaABC;
            barycentricClosestPoint = (X * A + Y * B + Z * C);
        }

        Vector3 A1 = _simplex.SupportPoints[faces[minIndex * 3]].Item1[0];
        Vector3 A2 = _simplex.SupportPoints[faces[minIndex * 3 + 1]].Item1[0];
        Vector3 A3 = _simplex.SupportPoints[faces[minIndex * 3 + 2]].Item1[0];

        Vector3 B1 = _simplex.SupportPoints[faces[minIndex * 3]].Item2[0];
        Vector3 B2 = _simplex.SupportPoints[faces[minIndex * 3 + 1]].Item2[0];
        Vector3 B3 = _simplex.SupportPoints[faces[minIndex * 3 + 2]].Item2[0];

        Vector3 contactPointA = (X * A1 + Y * A2 + Z * A3);
        Vector3 contactPointB = (X * B1 + Y * B2 + Z * B3);

        result.contactPoint = (contactPointA + contactPointB) / 2f;

        return result;
    }


    /// <returns>Closest face index and its distance to the origin</returns>
    private int GetClosestFace(List<Vector3> _points, List<int> _faces, ref List<Vector4> _normals, float tolerance)
    {
        int minFace = 0;
        float minDistance = float.MaxValue;

        for (int i = 0; i < _faces.Count; i += 3)
        {
            Vector3 a = _points[_faces[i]];
            Vector3 b = _points[_faces[i + 1]];
            Vector3 c = _points[_faces[i + 2]];

            // Normalize both segments, otherwise resulting cross product becomes Vector zero
            Vector4 normal = Vector3.Cross((b - a).normalized, (c - a).normalized).normalized;
            normal.w = Vector3.Dot(normal, a);

            if (normal.magnitude == 0f)
            {
                float length = Vector3.Dot(-a, (c - a).normalized);
                Vector3 point = a + length * (c - a).normalized;
                normal = point.normalized;
                normal.w = Vector3.Dot(normal, a);
            }

            // if normal is inward-facing, invert it to be outward
            float normalCheck = Vector3.Dot(normal, -a);
            if (normalCheck > tolerance)
                normal = -normal;
            else if (Mathf.Abs(normalCheck) <= tolerance)
            {
                int diffIndex = -1;

                if (i > 0)
                {
                    if (_faces[i - 1] != _faces[i] && _faces[i - 1] != _faces[i + 1] && _faces[i - 1] != _faces[i + 2])
                        diffIndex = i - 1;
                    else if (_faces[i - 2] != _faces[i] && _faces[i - 2] != _faces[i + 1] && _faces[i - 2] != _faces[i + 2])
                        diffIndex = i - 2;
                    else
                        diffIndex = i - 3;
                }
                else if (_faces.Count > i + 3)
                {
                    if (_faces[i + 3] != _faces[i] && _faces[i + 3] != _faces[i + 1] && _faces[i + 3] != _faces[i + 2])
                        diffIndex = i + 3;
                    else if (_faces[i + 4] != _faces[i] && _faces[i + 4] != _faces[i + 1] && _faces[i + 4] != _faces[i + 2])
                        diffIndex = i + 4;
                    else
                        diffIndex = i + 5;
                }
                if (diffIndex != -1)
                {
                    if (Vector3.Dot(normal, _points[_faces[diffIndex]]) > 0f)
                        normal = -normal;
                    if (normal.w < 0f)
                        normal.w = -normal.w;
                }
            }

            _normals.Add(normal);

            if (normal.w < minDistance)
            {
                minFace = i / 3;
                minDistance = normal.w;
            }
        }
        return minFace;
    }

    private void AddEdge(ref List<Tuple<int, int>> _uniqueEdges, List<int> _faces, int a, int b)
    {
        Tuple<int, int> reverse = _uniqueEdges.Find(x => x.Item1 == _faces[b] && x.Item2 == _faces[a]);
        if (reverse != null)
            _uniqueEdges.Remove(reverse);
        else
            _uniqueEdges.Add(Tuple.Create(_faces[a], _faces[b]));
    }

    private float GetSignedArea(Vector3 _A, Vector3 _B, Vector3 _C, Vector3 _normal)
    {
        float xAB = _A.x - _B.x;
        float yAB = _A.y - _B.y;
        float zAB = _A.z - _B.z;

        float xAC = _A.x - _C.x;
        float yAC = _A.y - _C.y;
        float zAC = _A.z - _C.z;

        // Recoding vector to avoid Unity Vector3 clamping to 3-digits precision
        List<float> vector = new List<float>();
        vector.Add(yAB * zAC - zAB * yAC);
        vector.Add(zAB * xAC - xAB * zAC);
        vector.Add(xAB * yAC - yAB * xAC);

        float magnitude = Mathf.Sqrt(vector[0] * vector[0] + vector[1] * vector[1] + vector[2] * vector[2]);
        if (magnitude == 0f)
            return 0f;

        float areaABC = magnitude / 2f;

        float dot = (_normal.x * (vector[0] / magnitude) + _normal.y * (vector[1] / magnitude) + _normal.z * (vector[2] / magnitude));
        float sAreaABC = dot * areaABC;

        return sAreaABC;
    }

    #endregion
}
