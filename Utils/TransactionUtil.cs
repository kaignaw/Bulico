using Autodesk.Revit.DB;

namespace Bulico
{
    internal static class TransactionUtil
    {
        /// <summary>
        /// 统一的失败处理设置：只删除指定的楼板重叠警告，并关闭强制模态对话框，
        /// 让所有事务的命令都遵循“不打断用户、失败即回滚”的约定。
        /// </summary>
        public static void Prepare(Transaction trans, bool suppressFloorOverlapWarnings)
        {
            if (trans == null)
                return;

            FailureHandlingOptions options = trans.GetFailureHandlingOptions();
            options.SetFailuresPreprocessor(new OverlapWarningSuppressor(suppressFloorOverlapWarnings));
            options.SetForcedModalHandling(false);
            trans.SetFailureHandlingOptions(options);
        }
    }
}
