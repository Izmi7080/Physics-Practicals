using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Editor-only import naming; leaves source FBX geometry and object identifiers intact.
public sealed class FrogAnatomyNames : AssetPostprocessor
{
    private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
    {
        { "下丘脑", "Hypothalamus" },
        { "中肾管", "Wolffian Duct" },
        { "嗅叶", "Olfactory Lobe" },
        { "大脑", "Cerebrum" },
        { "小管", "Tubule (Review)" },
        { "小脑", "Cerebellum" },
        { "巩膜", "Sclera" },
        { "心脏", "Heart" },
        { "漏斗管", "Infundibular Duct (Review)" },
        { "睾丸", "Testis" },
        { "肝", "Liver" },
        { "肠胃", "Stomach and Intestines" },
        { "肺", "Lung" },
        { "肺静脉", "Pulmonary Vein" },
        { "肾", "Kidney" },
        { "胆囊", "Gall Bladder" },
        { "胰腺", "Pancreas" },
        { "脂肪", "Fat Body" },
        { "脊髓", "Spinal Cord" },
        { "腹静脉", "Abdominal Vein" },
        { "膀胱", "Urinary Bladder" },
        { "视叶", "Optic Lobe" },
        { "间脑", "Diencephalon" },
        { "静脉", "Vein" },
        { "三角肌", "Deltoid" },
        { "下颌舌骨", "Mylohyoid" },
        { "下颌降肌", "Depressor Mandibulae" },
        { "前臂屈肌", "Forearm Flexor" },
        { "半膜肌", "Semimembranosus" },
        { "咬肌", "Masseter" },
        { "外展趾肌", "Toe Abductor" },
        { "对掌指肌", "Opponens Digitorum" },
        { "尺侧腕屈肌", "Flexor Carpi Ulnaris" },
        { "尾骨骼肌", "Coccygeal Muscle (Review)" },
        { "屈指肌", "Digital Flexor" },
        { "屈指肌腱", "Digital Flexor Tendon" },
        { "屈指骨", "Flexor Digit Structure (Review)" },
        { "拇内收肌", "Adductor Pollicis" },
        { "指伸肌", "Extensor Digitorum" },
        { "指层屈肌", "Digital Layer Flexor (Review)" },
        { "指屈肌", "Flexor Digitorum" },
        { "掌侧骨间", "Palmar Interosseous" },
        { "掌腱膜", "Palmar Aponeurosis" },
        { "桡侧伸腕肌", "Extensor Carpi Radialis" },
        { "皮胸肌", "Cutaneous Pectoral Muscle" },
        { "缝匠肌", "Sartorius" },
        { "肘肌", "Anconeus" },
        { "股中头三头肌", "Triceps Femoris Middle Head (Review)" },
        { "股后三头肌", "Triceps Femoris Posterior Head (Review)" },
        { "肩胛背肌", "Dorsalis Scapulae" },
        { "背最长肌", "Longissimus Dorsi" },
        { "背阔肌", "Latissimus Dorsi" },
        { "胫骨和小腿伸肌", "Tibial and Lower Leg Extensor" },
        { "胸上喙", "Pectoral Supracoracoid (Review)" },
        { "胸桡骨", "Pectoral Radial Structure (Review)" },
        { "胸腹肌", "Pectoralis Abdominalis" },
        { "胸骨肌", "Sternal Muscle" },
        { "腓肠肌", "Gastrocnemius" },
        { "腓骨肌", "Peroneus" },
        { "腹外斜肌", "External Oblique" },
        { "腹直肌", "Rectus Abdominis" },
        { "颞肌", "Temporalis" },
        { "髂外肌", "Iliacus Externus" },
        { "髂腰", "Iliopsoas" },
        { "头骨", "Skull" },
        { "寰椎", "Atlas" },
        { "尾杆骨", "Urostyle" },
        { "指骨", "Phalanx" },
        { "掌骨", "Metacarpal" },
        { "桡尺骨", "Radioulna" },
        { "椎骨", "Vertebra" },
        { "股骨", "Femur" },
        { "肩胛骨", "Scapula" },
        { "肱部", "Humerus" },
        { "胫腓骨", "Tibiofibula" },
        { "腕", "Carpal" },
        { "跖骨", "Metatarsal" },
        { "距骨", "Astragalus" },
        { "跟骨", "Calcaneum" },
        { "颚", "Jaw" },
        { "髂骨", "Ilium" },
    };

    public static bool IsTarget(string path)
    {
        // INTEGRATION (Tajarbagah): models moved from Assets/Models to Assets/_Ray/Models.
        // This rename is REQUIRED at runtime: the BiologyLab scripts find parts by these English names.
        return path == "Assets/_Ray/Models/Haslet.FBX" || path == "Assets/_Ray/Models/Muscle.fbx" || path == "Assets/_Ray/Models/Skeleton.FBX";
    }

    public static string Translate(string original)
    {
        string translated;
        if (Names.TryGetValue(original, out translated)) return translated;
        int separator = original.LastIndexOf(' ');
        int index;
        if (separator > 0 && int.TryParse(original.Substring(separator + 1), out index)
            && Names.TryGetValue(original.Substring(0, separator), out translated))
            return translated + original.Substring(separator);
        return original;
    }

    private void OnPostprocessModel(GameObject root)
    {
        if (!IsTarget(assetPath)) return;
        var renamedMeshes = new HashSet<Mesh>();
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root.transform) continue;
            child.name = Translate(child.name);
            // Shared meshes are renamed once, using their first owning anatomical part.
            MeshFilter filter = child.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null && renamedMeshes.Add(filter.sharedMesh))
                filter.sharedMesh.name = child.name;
            SkinnedMeshRenderer skinned = child.GetComponent<SkinnedMeshRenderer>();
            if (skinned != null && skinned.sharedMesh != null && renamedMeshes.Add(skinned.sharedMesh))
                skinned.sharedMesh.name = child.name;
        }
    }
}
