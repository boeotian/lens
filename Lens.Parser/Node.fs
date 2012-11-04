﻿module Lens.Parser.Node

open System
open System.Collections.Generic
open Lens.SyntaxTree.SyntaxTree
open Lens.SyntaxTree.SyntaxTree.ControlFlow
open Lens.SyntaxTree.SyntaxTree.Expressions
open Lens.SyntaxTree.SyntaxTree.Literals
open Lens.SyntaxTree.SyntaxTree.Operators
open Lens.SyntaxTree.Utils

type Accessor =
| Member of string
| Indexer of NodeBase

type Symbol =
| Static of string * string // type * name
| Local  of string

// Special nodes
let using nameSpace =
    UsingNode(Namespace = nameSpace) :> NodeBase

// Definitions
let typeTag nameSpace name additional =
    [nameSpace; Some name; additional]
    |> Seq.filter Option.isSome
    |> Seq.map Option.get
    |> String.concat(String.Empty)

let typeParams types =
    types
    |> String.concat ","
    |> sprintf "<%s>"

let arrayDefinition braces =
    braces
    |> Seq.map (fun _ -> "[]")
    |> String.concat String.Empty

let recordEntry(entryName, typeName) =
    RecordEntry(Name = entryName, Type = TypeSignature(typeName))

let record(name, entries) =
    let node = RecordDefinitionNode(Name = name)
    entries |> Seq.iter (fun e -> node.Entries.Add e)
    node :> NodeBase

let typeEntry(name, typeDefinition) =
    let signature =
        match typeDefinition with
        | Some s -> TypeSignature(s)
        | None   -> null
    TypeEntry(Name = name, TagType = signature)

let typeNode(name, entries) =
    let node = TypeDefinitionNode(Name = name)
    entries |> Seq.iter (fun e -> node.Entries.Add e)
    node :> NodeBase

let functionParameters parameters =
    let dictionary = Dictionary<_, _>()
    
    parameters
    |> Seq.map (fun((name, flag), typeTag) ->
                    let modifier =
                        match flag with
                        | Some "ref" -> ArgumentModifier.Ref
                        | Some "out" -> ArgumentModifier.Out
                        | _          -> ArgumentModifier.In
                    FunctionArgument(Name = name, Modifier = modifier, Type = TypeSignature(typeTag)))
    |> Seq.iter (fun fa -> dictionary.Add(fa.Name, fa))
    
    dictionary

let functionNode name parameters body =
    NamedFunctionNode(Name = name, Arguments = parameters, Body = body) :> NodeBase

// Code
let codeBlock (lines : NodeBase list) =
    CodeBlockNode(Statements = ResizeArray<_>(lines))

let variableDeclaration binding name value =
    let node : NameDeclarationBase =
        match binding with
        | "let" -> upcast LetNode()
        | "var" -> upcast VarNode()
        | _     -> failwith "Unknown value binding type"
    node.Name <- name
    node.Value <- value
    node :> NodeBase

let indexNode expression index : NodeBase =
    match index with
    | Some i -> upcast GetIndexNode(Expression = expression, Index = i)
    | None   -> expression
    

let getter : Accessor -> AccessorNodeBase = function
| Member(name)        -> upcast GetMemberNode(MemberName = name)
| Indexer(expression) -> upcast GetIndexNode(Index = expression)

/// Generates the getter chain and connects it to the node. accessors must be reversed.
let getterChain node (accessors : Accessor list) =
    List.fold
    <| (fun (n : AccessorNodeBase) a ->
        let newNode = getter a
        n.Expression <- newNode
        newNode)
    <| node
    <| accessors
    :?> GetMemberNode

let staticSymbol(typeName, symbolName) =
    Static(typeName, symbolName)

let localSymbol name =
    Local name

let getterNode symbol : NodeBase =
    match symbol with
    | Local name             -> upcast GetIdentifierNode(Identifier = name)
    | Static(typeName, name) -> upcast GetMemberNode(StaticType = TypeSignature typeName, MemberName = name)

let assignment (symbol : Symbol) accessorChain value : NodeBase =
    let setter symbol : NodeBase =
        match symbol with
        | Local name             -> upcast SetIdentifierNode(Identifier = name, Value = value)
        | Static(typeName, name) -> upcast SetMemberNode(
                                        StaticType = TypeSignature typeName,
                                        MemberName = name,
                                        Value = value)
    let accessorSetter : Accessor -> AccessorNodeBase = function
    | Member  name       -> upcast SetMemberNode(MemberName = name, Value = value)
    | Indexer expression -> upcast SetIndexNode(Index = expression, Value = value)
    
    match accessorChain with
    | [] -> setter symbol
    | _  -> let accessors = List.rev accessorChain
            let root = accessorSetter <| List.head accessors
            let last = getterChain root <| List.tail accessors
            let top = getterNode symbol
            last.Expression <- top
            upcast last

let lambda parameters code : NodeBase =
    let node = FunctionNode(Body = code)
    Option.iter
    <| fun p -> node.Arguments <- p
    <| parameters
    upcast node

let invocation expression (parameters : NodeBase list) : NodeBase =
    upcast InvocationNode(Expression = expression, Arguments = ResizeArray<_> parameters)

// Branch constructions
let ifNode condition thenBlock elseBlock =
    let falseAction =
        match elseBlock with
        | Some a -> a
        | None   -> null
    ConditionNode(Condition = condition, TrueAction = thenBlock, FalseAction = falseAction) :> NodeBase

let whileNode condition block =
    LoopNode(Condition = condition, Body = block) :> NodeBase

let tryCatchNode expression catchClauses =
    let node = TryNode(Code = expression)
    node.CatchClauses.AddRange(catchClauses)
    node :> NodeBase

let catchNode variableDefinition code =
    let node =
        match variableDefinition with
        | Some (typeName, variableName) -> CatchNode(
                                               ExceptionType = TypeSignature(typeName),
                                               ExceptionVariable = variableName)
        | None                          -> CatchNode()
    node.Code <- code
    node

// Literals
let unit _ =
    failwith<NodeBase> "Unit literal not supported"

let nullNode _ =
    failwith<NodeBase> "Null literal not supported"

let boolean value =
    let v = 
        match value with
        | "true"  -> true
        | "false" -> false
        | other   -> failwithf "Unknown boolean value %s" other
    BooleanNode(Value = v) :> NodeBase

let int (value : string) =
    IntNode(Value = int value) :> NodeBase

let double (value : string) =
    DoubleNode(Value = double value) :> NodeBase

let string value =
    StringNode(Value = value) :> NodeBase

// Operators
let castNode expression typeName =
    match typeName with
    | None      -> expression
    | Some name -> CastOperatorNode(Expression = expression, Type = TypeSignature name) :> NodeBase

let binaryOperatorNode symbol : BinaryOperatorNodeBase =
    let booleanKind = function
    | "&&"  -> BooleanOperatorKind.And
    | "||"  -> BooleanOperatorKind.Or
    | "^^"  -> BooleanOperatorKind.Xor
    | other -> failwithf "Unknown boolean operator kind %s" other

    let comparisonKind = function
    | "==" -> ComparisonOperatorKind.Equals
    | "<>" -> ComparisonOperatorKind.NotEquals
    | "<"  -> ComparisonOperatorKind.Less
    | ">"  -> ComparisonOperatorKind.Greater
    | "<=" -> ComparisonOperatorKind.LessEquals
    | ">=" -> ComparisonOperatorKind.GreaterEquals
    | other -> failwithf "Unknown comparison operator kind %s" other

    match symbol with
    | "&&"
    | "||"
    | "^^" -> upcast BooleanOperatorNode(Kind = booleanKind symbol)
    | "=="
    | "<>"
    | "<"
    | ">"
    | "<="
    | ">=" -> upcast ComparisonOperatorNode(Kind = comparisonKind symbol)
    | "**" -> upcast PowOperatorNode()
    | "*"  -> upcast MultiplyOperatorNode()
    | "/"  -> upcast DivideOperatorNode()
    | "%"  -> upcast RemainderOperatorNode()
    | "+"  -> upcast AddOperatorNode()
    | "-"  -> upcast SubtractOperatorNode()
    | _    -> failwithf "Unknown binary operator %s" symbol

let unaryOperator symbol operand : NodeBase =
    match symbol with
    | Some "not" -> upcast InversionOperatorNode(Operand = operand)
    | Some "-"   -> upcast NegationOperatorNode(Operand = operand)
    | Some other -> failwithf "Unknown unary operator %s" other
    | None       -> operand

let private binaryOperator symbol left right =
    let node = binaryOperatorNode symbol
    node.LeftOperand <- left
    node.RightOperand <- right
    node :> NodeBase

let rec operatorChain node operations =
    match operations with
    | [] -> node
    | (op, node2) :: other ->
        let newNode = binaryOperator op node node2
        operatorChain newNode other

let typeOperator symbol typeName =
    let node : TypeOperatorNodeBase =
        match symbol with
        | "typeof"  -> upcast TypeofOperatorNode()
        | "default" -> upcast DefaultOperatorNode()
        | other     -> failwithf "Unknown type operator %s" other
    node.Type <- TypeSignature typeName
    node :> NodeBase

// New objects
let dictEntry key value =
    KeyValuePair(key, value)

let objectNode typeName (parameters : NodeBase list option) =
    let arguments =
        match parameters with
        | Some args -> ResizeArray<_> args
        | None      -> ResizeArray<_>()
    NewObjectNode(Type = TypeSignature typeName, Arguments = arguments) :> NodeBase

let tupleNode (elements : NodeBase list) =
    NewTupleNode(Expressions = ResizeArray<_> elements) :> NodeBase

let listNode (elements: NodeBase list) : NodeBase =
    upcast NewListNode(Expressions = ResizeArray<_> elements)

let dictNode (elements : KeyValuePair<NodeBase, NodeBase> list) : NodeBase =
    upcast NewDictionaryNode(Expressions = ResizeArray<_> elements)

let arrayNode (elements : NodeBase list) =
    NewArrayNode(Expressions = ResizeArray<_> elements) :> NodeBase
