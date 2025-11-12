using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DA.Game.Application.Shared.Primitives;

public interface IQuery<TRes> : MediatR.IRequest<TRes> { }