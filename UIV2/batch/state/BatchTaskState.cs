using InfectionCardReplacer.UIV2.Batch.Models;

namespace InfectionCardReplacer.UIV2.Batch.State;

internal sealed class BatchTaskState
{
    public BatchTaskGenerateOptions CurrentOptions { get; set; } = new();
    public List<BatchTaskSourceOption> SourceOptions { get; set; } = [];
    public List<BatchTaskEntry> Entries { get; set; } = [];
    public int SelectedIndex { get; set; } = -1;
    public string StatusMessage { get; set; } = "等待生成批量任务";
}
