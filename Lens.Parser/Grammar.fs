﻿module Lens.Parser.Grammar

open FParsec
open FParsec.CharParsers
open Lens.SyntaxTree.SyntaxTree

let stmt, stmtRef                         = createParserForwardedToRef()
let using, usingRef                       = createParserForwardedToRef()
let ``namespace``, namespaceRef           = createParserForwardedToRef()
let recorddef, recorddefRef               = createParserForwardedToRef()
let recorddef_stmt, recorddef_stmtRef     = createParserForwardedToRef()
let typedef, typedefRef                   = createParserForwardedToRef()
let typedef_stmt, typedef_stmtRef         = createParserForwardedToRef()
let funcdef, funcdefRef                   = createParserForwardedToRef()
let func_params, func_paramsRef           = createParserForwardedToRef()
let block, blockRef                       = createParserForwardedToRef()
let block_line, block_lineRef             = createParserForwardedToRef()
let ``type``, typeRef                     = createParserForwardedToRef()
let local_stmt, local_stmtRef             = createParserForwardedToRef()
let assign_expr, assign_exprRef           = createParserForwardedToRef()
let rvalue, rvalueRef                     = createParserForwardedToRef()
let accessor_expr, accessor_exprRef       = createParserForwardedToRef()
let expr, exprRef                         = createParserForwardedToRef()
let block_expr, block_exprRef             = createParserForwardedToRef()
let if_expr, if_exprRef                   = createParserForwardedToRef()
let while_expr, while_exprRef             = createParserForwardedToRef()
let try_expr, try_exprRef                 = createParserForwardedToRef()
let catch_expr, catch_exprRef             = createParserForwardedToRef()
let lambda_expr, lambda_exprRef           = createParserForwardedToRef()
let line_expr, line_exprRef               = createParserForwardedToRef()
let line_expr_1, line_expr_1Ref           = createParserForwardedToRef()
let sign_1, sign_1Ref                     = createParserForwardedToRef()
let line_expr_2, line_expr_2Ref           = createParserForwardedToRef()
let sign_2, sign_2Ref                     = createParserForwardedToRef()
let line_expr_3, line_expr_3Ref           = createParserForwardedToRef()
let sign_3, sign_3Ref                     = createParserForwardedToRef()
let line_expr_4, line_expr_4Ref           = createParserForwardedToRef()
let sign_4, sign_4Ref                     = createParserForwardedToRef()
let line_expr_5, line_expr_5Ref           = createParserForwardedToRef()
let line_expr_6, line_expr_6Ref           = createParserForwardedToRef()
let line_expr_7, line_expr_7Ref           = createParserForwardedToRef()
let new_expr, new_exprRef                 = createParserForwardedToRef()
let new_array_expr, new_array_exprRef     = createParserForwardedToRef()
let new_tuple_expr, new_tuple_exprRef     = createParserForwardedToRef()
let new_list_expr, new_list_exprRef       = createParserForwardedToRef()
let new_dict_expr, new_dict_exprRef       = createParserForwardedToRef()
let new_obj_expr, new_obj_exprRef         = createParserForwardedToRef()
let enumeration_expr, enumeration_exprRef = createParserForwardedToRef()
let dict_entry_expr, dict_entry_exprRef   = createParserForwardedToRef()
let invoke_expr, invoke_exprRef           = createParserForwardedToRef()
let invoke_list, invoke_listRef           = createParserForwardedToRef()
let value_expr, value_exprRef             = createParserForwardedToRef()
let typeof_expr, typeof_exprRef           = createParserForwardedToRef()
let literal, literalRef                   = createParserForwardedToRef()
    
let string, stringRef                     = createParserForwardedToRef()
let identifier, identifierRef             = createParserForwardedToRef()
let whitespace1 = many1 <| anyOf " \t"

let main             = many stmt .>> eof
stmtRef             := using <|> recorddef <|> typedef <|> funcdef <|> local_stmt
usingRef            := pstring "using" >>. ``namespace`` .>> newline |>> Node.using
namespaceRef        := sepBy1 identifier <| pstring "::"
recorddefRef        := (pstring "record" .>> whitespace1) >>. identifier .>>. IndentationParser.indentedMany1 recorddef_stmt "recorddef_stmt" |>> Node.record
recorddef_stmtRef   := (identifier .>>. (skipChar ':' >>. ``type``)) |>> Node.recordEntry
typedefRef          := pzero<NodeBase, ParserState> (* TODO: "type" identifier NL typedef_stmt { typedef_stmt } *)
typedef_stmtRef     := pzero<NodeBase, ParserState> (* TODO: INDENT "|" identifier [ "of" type ] NL *)
funcdefRef          := pzero<NodeBase, ParserState> (* TODO: "fun" identifier func_params "->" block *)
func_paramsRef      := pzero<NodeBase, ParserState> (* TODO: { identifier ":" [ ( "ref" | "out" ) ] type } *)
blockRef            := pzero<NodeBase, ParserState> (* TODO: NL block_line { block_line } | line_expr *)
block_lineRef       := pzero<NodeBase, ParserState> (* TODO: INDENT local_stmt NL *)
typeRef             := (* TODO: [ namespace "." ] *) identifier (* TODO: [ ( { "[]" } | "<" type ">" ) ] *)
local_stmtRef       := (* TODO: assign_expr | *) expr
assign_exprRef      := pzero<NodeBase, ParserState> (* ( [ "let" | "var" ] identifier | rvalue ) "=" expr *)
rvalueRef           := pzero<NodeBase, ParserState> (* ( type | "(" line_expr ")" ) accessor_expr { accessor_expr } *)
accessor_exprRef    := pzero<NodeBase, ParserState> (* "." identifier | "[" line_expr "]" *)
exprRef             := (* TODO: block_expr | *) line_expr
block_exprRef       := pzero<NodeBase, ParserState> (* if_expr | while_expr | try_expr | lambda_expr *)
if_exprRef          := pzero<NodeBase, ParserState> (* "if" "(" line_expr ")" block [ "else" block ] *)
while_exprRef       := pzero<NodeBase, ParserState> (* "while" "(" line_expr ")" block *)
try_exprRef         := pzero<NodeBase, ParserState> (* "try" block catch_expr { catch_expr } *)
catch_exprRef       := pzero<NodeBase, ParserState> (* "catch" [ "(" type identifier ")" ] block *)
lambda_exprRef      := pzero<NodeBase, ParserState> (* [ "(" func_params ")" ] "->" block *)
line_exprRef        := line_expr_1 (* TODO: [ "as" type ] *)
line_expr_1Ref      := line_expr_2 (* TODO: { sign_1 line_expr_2 } *)
sign_1Ref           := pzero<NodeBase, ParserState> (* TODO: "&&" | "||" | "^^" *)
line_expr_2Ref      := line_expr_3 (* TODO: { sign_2 line_expr_3 } *)
sign_2Ref           := pzero<NodeBase, ParserState> (* TODO: "==" | "<>" | "<" | ">" | "<=" | ">=" *)
line_expr_3Ref      := (* TODO: [ "not" | "-" ] *) (line_expr_4 .>>. (many (sign_3 .>>. line_expr_4))) |>> Node.operatorChain
sign_3Ref           := pstring "+" <|> pstring "-"
line_expr_4Ref      := line_expr_5 (* TODO: { sign_4 line_expr_5 } *)
sign_4Ref           := pzero<NodeBase, ParserState> (* TODO: "*" | "/" | "%" *)
line_expr_5Ref      := line_expr_6 (* TODO: { "**" line_expr_6 } *)
line_expr_6Ref      := line_expr_7 (* TODO: { "[" expr "]" } *)
line_expr_7Ref      := (* TODO: new_expr | *) invoke_expr
new_exprRef         := pzero<NodeBase, ParserState> (* TODO: "new" ( new_array_expr | new_tuple_expr | new_list_expr | new_dictionary_expr | new_obj_expr ) *)
new_array_exprRef   := pzero<NodeBase, ParserState> (* TODO: "[" enumeration_expr "]" *)
new_tuple_exprRef   := pzero<NodeBase, ParserState> (* TODO: "(" enumeration_expr ")" *)
new_list_exprRef    := pzero<NodeBase, ParserState> (* TODO: "<" enumeration_expr ">" *)
new_dict_exprRef    := pzero<NodeBase, ParserState> (* TODO: "{" dict_entry_expr { ";" dict_entry_expr } "}" *)
new_obj_exprRef     := pzero<NodeBase, ParserState> (* TODO: type [ invoke_expr ] *)
enumeration_exprRef := pzero<NodeBase, ParserState> (* TODO: line_expr { ";" line_expr } *)
dict_entry_exprRef  := pzero<NodeBase, ParserState> (* TODO: line_expr "=>" line_expr *)
invoke_exprRef      := value_expr (* TODO: [ invoke_list ] *)
invoke_listRef      := pzero<NodeBase, ParserState> (* TODO: { value_expr } | ( { NL "<|" value_expr } NL ) *)
value_exprRef       := (* TODO: type { typeof_expr | accessor_expr } | *) literal (* TODO: | "(" expr ")" *)
typeof_exprRef      := pzero<NodeBase, ParserState> (* TODO: "typeof" "(" type ")" *)
literalRef          := (* TODO: "()" | "null" | "true" | "false" | string | *) regex "\d+" |>> Node.int

stringRef           := pzero<NodeBase, ParserState> (* TODO: ... *)
identifierRef       := regex "[a-zA-Z_]+"
