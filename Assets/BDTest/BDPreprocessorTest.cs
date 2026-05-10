using UnityEngine;

/// <summary>
/// 预处理器级别检测 GRAPH_DESIGNER
/// </summary>
public class BDPreprocessorTest : MonoBehaviour
{
    void Start()
    {
#if GRAPH_DESIGNER
        Debug.Log("[BDTest] GRAPH_DESIGNER IS defined —— 预处理器级别通过");
#else
        Debug.Log("[BDTest] GRAPH_DESIGNER is NOT defined —— 预处理器级别失败");
#endif
    }
}
