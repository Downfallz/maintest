using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Learning.Abstractions;

// Abstractions/ITrainer.cs
public interface ITrainer
{
    void Train(string datasetPath, string modelPath);
}
