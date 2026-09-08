using DA.Game.Application.Learning.Abstractions;
using DA.Game.Application.Learning.ML.Models;

using Microsoft.ML;

namespace DA.Game.Application.Learning.ML;

public sealed class MLTrainer : ITrainer
{
    public void Train(string datasetPath, string modelPath)
    {
        var ml = new MLContext(seed: 123);

        // Charger le dataset
        var data = ml.Data.LoadFromTextFile<GameModelInput>(datasetPath, hasHeader: true, separatorChar: ',');

        // Quick preview to validate content and labels
        var preview = data.Preview(maxRows: 20);
        if (preview.RowView.Length == 0)
            throw new InvalidOperationException($"Training CSV '{datasetPath}' contains no data rows. Confirm the file and separator.");

        // Check for at least one non-empty label value in preview
        var labelCol = nameof(GameModelInput.Action);
        bool hasLabel = preview.RowView.Any(rv =>
            rv.Values.Any(kv =>
                string.Equals(kv.Key, labelCol, StringComparison.OrdinalIgnoreCase)
                && kv.Value is not null
                && !string.IsNullOrWhiteSpace(kv.Value.ToString())
            )
        );

        if (!hasLabel)
            throw new InvalidOperationException($"Training CSV '{datasetPath}' contains no non-empty values in label column '{labelCol}'. Ensure the logger writes Action values.");

        // Pipeline : features → clé → classifier
        var pipeline = ml.Transforms.Conversion.MapValueToKey("Label", nameof(GameModelInput.Action))
            .Append(ml.Transforms.Concatenate("Features", nameof(GameModelInput.Turn),
                                                           nameof(GameModelInput.PlayerHealth),
                                                           nameof(GameModelInput.EnemyHealth),
                                                           nameof(GameModelInput.PlayerEnergy)))
            .Append(ml.MulticlassClassification.Trainers.SdcaMaximumEntropy())
            .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

        var model = pipeline.Fit(data);
        ml.Model.Save(model, data.Schema, modelPath);
    }
}
