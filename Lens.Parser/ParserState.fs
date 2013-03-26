﻿namespace Lens.Parser

type ParserState = {
    RealIndentation : int
    VirtualIndentation : int
}
    with
        static member Create() = { RealIndentation = 0
                                   VirtualIndentation = 0 }
