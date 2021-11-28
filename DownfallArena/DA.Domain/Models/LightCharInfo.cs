using System;
using DA.Game.Domain.Models.Enum;

namespace DA.Game.Domain.Models;

public record LightCharInfo
{
    public Guid Id { get; set; }
    public TeamIndicator Team { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}