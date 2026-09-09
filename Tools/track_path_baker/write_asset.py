import json
d = json.load(open("track_path.json"))
pts, half, lap = d["points"], d["halfWidth"], d["lapLength"]
L = ["%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
     "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}",
     "  m_PrefabInstance: {fileID: 0}", "  m_PrefabAsset: {fileID: 0}",
     "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
     "  m_Script: {fileID: 11500000, guid: 7c3f1a52e0b84d24a9f6c1d80b3e5f11, type: 3}",
     "  m_Name: TrackPathSO", "  m_EditorClassIdentifier: Assembly-CSharp::TrackPathSO",
     "  points:"]
L += ["  - {x: %.3f, y: %.3f}" % (x, z) for x, z in pts]
L.append("  halfWidth: %.3f" % half)
L.append("  closedLoop: 1")        # the baked track is a circuit
L.append("  lapLength: %.3f" % lap)
p = r"C:\_DarkMatter\UnityProjects\Bajaj Racing Game\Assets\Darkmatter\Scripts\Gameplay\TrackPathSO.asset"
open(p, "w", newline="\n").write("\n".join(L) + "\n")
print("wrote %d points, lap %.1f units, half width %.2f (road %.2f wide)" % (len(pts), lap, half, 2 * half))
