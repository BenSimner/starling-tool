/// <summary>
///     Functions and types for commands.
/// </summary>
module Starling.Core.Command

open Starling.Utils
open Starling.Collections
open Starling.Core.TypeSystem
open Starling.Core.Expr
open Starling.Core.Var
open Starling.Core.Symbolic
open Starling.Core.Model


/// <summary>
///     Types for commands.
/// </summary>
[<AutoOpen>]
module Types =
    /// <summary>
    ///     A command.
    ///
    ///     <para>
    ///         A command is a list, representing a sequential composition
    ///         of commands
    ///     </para>
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Command's have format
    ///             [Result] <- CmdName([Params])
    ///
    ///         Where Result = the changed local variables (list of Var)
    ///         Params = the expressions inside the command
    ///     </para>
    /// </remarks>

    type CommandType = 
        { Results: Var list; Params: Expr<Sym<Var>> list; CmdName: string }

    type Command = CommandType list


    /// <summary>
    ///     A term over <c>Command</c>s.
    /// </summary>
    /// <typeparam name="wpre">
    ///     The type of the weakest-precondition part of the term.
    /// </typeparam>
    /// <typeparam name="goal">
    ///     The type of the goal part of the term.
    /// </typeparam>
    type PTerm<'wpre, 'goal> = Term<Command, 'wpre, 'goal>


/// <summary>
///     Queries on commands.
/// </summary>
module Queries =
    /// <summary>
    ///     Decides whether a program command is a no-op.
    /// </summary>
    /// <param name="_arg1">
    ///     The command, as a <c>Command</c>.
    /// </param>
    /// <returns>
    ///     <c>true</c> if the command is a no-op;
    ///     <c>false</c> otherwise.
    /// </returns>
    let isNop =
        List.forall
            // Commands are no-op's if they modify no vars
            <| fun { Results = ps } -> ps = []

    /// <summary>
    ///     Active pattern matching assume commands.
    /// </summary>
    let (|Assume|_|) =
        function
        | [ { CmdName = n ; Params = [ Expr.Bool b ] ; Results = [] } ]
          when n = "Assume" -> Some b
        | _ -> None


/// <summary>
///     Composition of Boolean expressions representing commands.
/// </summary>
module Compose =
    /// <summary>
    ///     Finds the highest intermediate stage number in an integral
    ///     expression.
    ///     Returns one higher.
    /// </summary>
    /// <param name="_arg1">
    ///     The <c>IntExpr</c> to investigate.
    /// </param>
    /// <returns>
    ///     The next available intermediate stage number.
    ///     If the expression has no intermediate stages, we return 0.
    /// </returns>
    let rec nextIntIntermediate =
        function
        | AVar (Reg (Intermediate (n, _))) -> n + 1I
        | AVar (Sym { Params = xs } ) ->
            xs |> Seq.map nextIntermediate |> Seq.fold (curry bigint.Max) 0I
        | AVar _ | AInt _ -> 0I
        | AAdd xs | ASub xs | AMul xs ->
            xs |> Seq.map nextIntIntermediate |> Seq.fold (curry bigint.Max) 0I
        | ADiv (x, y) ->
            bigint.Max (nextIntIntermediate x, nextIntIntermediate y)

    /// <summary>
    ///     Finds the highest intermediate stage number in a Boolean expression.
    ///     Returns one higher.
    /// </summary>
    /// <param name="_arg1">
    ///     The <c>BoolExpr</c> to investigate.
    /// </param>
    /// <returns>
    ///     The next available intermediate stage number.
    ///     If the expression has no intermediate stages, we return 0.
    /// </returns>
    and nextBoolIntermediate =
        function
        | BVar (Reg (Intermediate (n, _))) -> n + 1I
        | BVar (Sym { Params = xs } ) ->
            xs |> Seq.map nextIntermediate |> Seq.fold (curry bigint.Max) 0I
        | BVar _ -> 0I
        | BAnd xs | BOr xs ->
            xs |> Seq.map nextBoolIntermediate |> Seq.fold (curry bigint.Max) 0I
        | BImplies (x, y) ->
            bigint.Max (nextBoolIntermediate x, nextBoolIntermediate y)
        | BNot x -> nextBoolIntermediate x
        | BGt (x, y) | BLt (x, y) | BGe (x, y) | BLe (x, y) ->
            bigint.Max (nextIntIntermediate x, nextIntIntermediate y)
        | BEq (x, y) ->
            bigint.Max (nextIntermediate x, nextIntermediate y)
        | BTrue | BFalse -> 0I

    /// <summary>
    ///     Finds the highest intermediate stage number in an expression.
    ///     Returns one higher.
    /// </summary>
    /// <param name="_arg1">
    ///     The <c>Expr</c> to investigate.
    /// </param>
    /// <returns>
    ///     The next available intermediate stage number.
    ///     If the expression has no intermediate stages, we return 0.
    /// </returns>
    and nextIntermediate =
        function
        | Int x -> nextIntIntermediate x
        | Bool x -> nextBoolIntermediate x


/// <summary>
///     Functions for removing symbols from commands.
///
///     <para>
///         We take the approach that, for a command, whenever we see a
///         statement <c>_ = %{sym}(...)</c> or <c>%{sym}(...) = _</c>,
///         we can drop that statement (leaving the assignment
///         non-deterministic).  This distributes through conjunctions,
///         and the RHS of implications, but not through any other
///         operations.  (It probably can, but for now we're being
///         conservative.)
///     </para>
/// </para>
module SymRemove =
    /// <summary>
    ///     Active pattern matching likely assignments to or from symbols.
    /// </summary>
    let (|SymAssign|_|) =
        // TODO(CaptainHayashi): sound and/or complete?
        function
        | BEq ((Expr.Bool (BVar (Sym _)) as lhs),
               (Expr.Bool _ as rhs))
        | BEq ((Expr.Bool _ as lhs),
               (Expr.Bool (BVar (Sym _)) as rhs))
        | BEq ((Expr.Int (AVar (Sym _)) as lhs),
               (Expr.Int _ as rhs))
        | BEq ((Expr.Int _ as lhs),
               (Expr.Int (AVar (Sym _)) as rhs)) -> Some (lhs, rhs)
        | _ -> None

    /// <summary>
    ///     Tries to remove symbolic assignments from a command in
    ///     Boolean expression form.
    /// </summary>
    let rec removeSym : SMBoolExpr -> SMBoolExpr =
        function
        | SymAssign (_, _) -> BTrue
        // Distributivity.
        // TODO(CaptainHayashi): distribute through more things?
        | BAnd xs -> BAnd (List.map removeSym xs)
        | BImplies (lhs, rhs) -> BImplies (lhs, removeSym rhs)
        // Anything else passes through unchanged.
        | x -> x


/// <summary>
///     Pretty printers for commands.
/// </summary>
module Pretty =
    open Starling.Core.Pretty
    open Starling.Core.Var.Pretty
    open Starling.Core.Expr.Pretty
    open Starling.Core.Model.Pretty
    open Starling.Core.Symbolic.Pretty

    /// Pretty-prints a Command.
    let printCommandType { CmdName = name; Params = xs; Results = ys } = 
        hjoin [ commaSep <| Seq.map String ys; "<-" |> String; name |> String; commaSep <| Seq.map printSVExpr xs ]

    let printCommand = List.map printCommandType >> semiSep

    /// Pretty-prints a PTerm.
    let printPTerm pWPre pGoal = printTerm printCommand pWPre pGoal
