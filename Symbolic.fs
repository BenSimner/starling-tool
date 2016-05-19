/// <summary>
///     Symbolic variables, and functions for dealing with them.
///
///     <para>
///         Symbolic variables (<c>Sym</c>) are how Starling encodes
///         arbitrary functions on zero or more variables that involve
///         syntax or concepts Starling can't internally handle.
///     </para>
///     <para>
///         They overload the variable position in expressions with a
///         disjunction between regular variables and uninterpreted,
///         arbitrary strings.  These strings are parameterised by
///         expression variables, as if they were method calls.
///         However, they represent a textual substitution of the
///         given variables into the string.
///     </para>
///     <para>
///         Starling proofs using symbolic variables cannot be proven
///         automatically.  Instead, the symbols must either be removed,
///         or replaced with some other Starling construct.  The typemap
///         <c>tryRemoveSym</c> tries to remove all <c>Sym</c>s from
///         expressions, failing if any exist.  The function
///         <c>approxSym</c> substitutes <c>true</c> and <c>false</c> for
///         symbols in Boolean positions, depending on whether they arise
///         in a positive or negative position.
///     </para>
/// </summary>
module Starling.Core.Symbolic

open Chessie.ErrorHandling
open Starling.Collections
open Starling.Utils
open Starling.Core.TypeSystem
open Starling.Core.Expr
open Starling.Core.Var
open Starling.Core.Sub


/// <summary>
///     Types for symbolic and variable maps.
/// </summary>
[<AutoOpen>]
module Types =
    /// <summary>
    ///     A variable reference that may be symbolic.
    ///
    ///     <para>
    ///         A symbolic variable is one whose value depends on an
    ///         uninterpreted function of multiple 'real' Starling variables.
    ///         It allows encoding into Starling of patterns of variable usage
    ///         Starling doesn't yet understand natively.
    ///     </para>
    /// </summary>
    /// <typeparam name="var">
    ///     The non-symbolic variable <c>Sym</c> wraps.
    /// </typeparam>
    type Sym<'var> =
        /// <summary>
        ///     A symbolic variable, predicated over multiple expressions.
        ///     The symbol itself is the name inside the <c>Func</c>.
        /// </summary>
        | Sym of Func<Expr<Sym<'var>>>
        /// <summary>
        ///     A regular, non-symbolic variable.
        | Reg of 'var


/// <summary>
///     Type synonyms for expressions over various forms of symbolic
///     variable.
/// </summary>
[<AutoOpen>]
module SymExprs =
    /// <summary>
    ///     An expression of arbitrary type using symbolic <c>Var</c>s.
    /// </summary>
    type SVExpr = Expr<Sym<Var>>
    /// <summary>
    ///     An expression of Boolean type using symbolic <c>Var</c>s.
    /// </summary>
    type SVBoolExpr = BoolExpr<Sym<Var>>
    /// <summary>
    ///     An expression of integral type using <c>Var</c>s.
    /// </summary>
    type SVIntExpr = IntExpr<Sym<Var>>

    /// <summary>
    ///     An expression of arbitrary type using symbolic <c>MarkedVar</c>s.
    /// </summary>
    type SMExpr = Expr<Sym<MarkedVar>>
    /// <summary>
    ///     An expression of Boolean type using symbolic <c>MarkedVar</c>s.
    /// </summary>
    type SMBoolExpr = BoolExpr<Sym<MarkedVar>>
    /// <summary>
    ///     An expression of integral type using symbolic <c>MarkedVar</c>s.
    /// </summary>
    type SMIntExpr = IntExpr<Sym<MarkedVar>>


/// <summary>
///     Utilities for creating symbolic variables.
/// </summary>
[<AutoOpen>]
module Create =
    /// Creates an integer sym-variable.
    let siVar c = c |> Reg |> AVar

    /// Creates an before-marked integer sym-variable.
    let siBefore c = c |> Before |> Reg |> AVar

    /// Creates an after-marked integer sym-variable.
    let siAfter c = c |> After |> Reg |> AVar

    /// Creates a goal-marked integer sym-variable.
    let siGoal i c = (i, c) |> Goal |> Reg |> AVar

    /// Creates an intermediate-marked integer sym-variable.
    let siInter i c = (i, c) |> Intermediate |> Reg |> AVar

    /// Creates a Boolean sym-variable.
    let sbVar c = c |> Reg |> BVar

    /// Creates an before-marked Boolean sym-variable.
    let sbBefore c = c |> Before |> Reg |> BVar

    /// Creates an before-marked Boolean sym-variable.
    let sbAfter c = c |> After |> Reg |> BVar

    /// Creates a goal-marked Boolean sym-variable.
    let sbGoal i c = (i, c) |> Goal |> Reg |> BVar

    /// Creates an intermediate-marked Boolean sym-variable.
    let sbInter i c = (i, c) |> Intermediate |> Reg |> BVar


/// <summary>
///     Utilities to traverse or eliminate symbolic variables.
/// </summary>
[<AutoOpen>]
module Queries =
    /// <summary>
    ///     Extracts all of the regular variables in a symbolic variable.
    /// </summary>
    /// <param name="sym">
    ///     The symbolic variable to destructure.
    /// </param>
    /// <typeparam name="var">
    ///     The type of regular variables in the symbolic variable.
    /// </typeparam>
    /// <returns>
    ///     A list of regular variables bound in a symbolic variable.
    /// </returns>
    let rec varsInSym
      (sym : Sym<'var>)
      : 'var list =
        match sym with
        | Reg x -> [x]
        | Sym { Params = xs } ->
            xs
            |> List.map (varsIn
                         >> Set.toList
                         >> List.map (valueOf >> varsInSym)
                         >> List.concat)
            |> List.concat

    /// Lifts a VSubFun over MarkedVars to deal with symbolic vars.
    let rec liftVToSym
      (sf : VSubFun<'srcVar, Sym<'dstVar>>)
      : VSubFun<Sym<'srcVar>, Sym<'dstVar>> =
        let rmap ctx =
            (sf |> liftVToSym |> onVars |> Mapper.mapCtx) ctx

        Mapper.makeCtx
            (fun pos v ->
                 match v with
                 | Reg r -> Mapper.mapIntCtx sf pos r
                 | Sym { Name = sym; Params = rs } ->
                     // TODO(CaptainHayashi): this is horrible.
                     // Are our abstractions wrong?
                     let pos', rs' = mapAccumL rmap pos rs
                     (pos', AVar (Sym { Name = sym; Params = rs' } )))
            (fun pos v ->
                 match v with
                 | Reg r -> Mapper.mapBoolCtx sf pos r
                 | Sym { Name = sym; Params = rs } ->
                     let pos', rs' = mapAccumL rmap pos rs
                     (pos', BVar (Sym { Name = sym; Params = rs' } )))

    /// <summary>
    ///     Substitution table for removing symbols from expressions.
    /// </summary>
    /// <param name="err">
    ///     Function mapping a symbol's contents to an error to throw when
    ///     detecting one.
    /// </param>
    /// <typeparam name="err">
    ///     The type of <paramref name="err"/>.
    /// </typeparam>
    /// <typeparam name="err">
    ///     The type of regular (non-symbolic) variables.
    /// </typeparam>
    /// <returns>
    ///     A <c>TrySubFun</c> trying to remove symbols.
    /// </returns>
    let tsfRemoveSym
      (err : string -> 'err)
      : TrySubFun<Sym<'var>, 'var, 'err> =
        tryOnVars <| Mapper.make
            (function
             | Sym s -> s.Name |> err |> fail
             | Reg f -> f |> AVar |> ok)
            (function
             | Sym s -> s.Name |> err |> fail
             | Reg f -> f |> BVar |> ok)

    (*
     * Common substitutions
     *)

    /// <summary>
    ///     Converts a marking <c>CMapper</c> to a <c>SubFun</c> over
    ///     symbolic variables.
    /// </summary>
    /// <param name="mapper">
    ///     The variable <c>CMapper</c> to lift.
    /// </param>
    /// <typeparam name="srcVar">
    ///     The type of variables entering the map.
    /// </typeparam>
    /// <typeparam name="dstVar">
    ///     The type of variables leaving the map.
    /// </typeparam>
    /// <returns>
    ///     <paramref name="mapper">, lifted into a <C>SubFun</c>
    ///     over symbolic variables.
    /// </returns>
    let liftCToSymSub
      (mapper : CMapper<SubCtx, 'srcVar, 'dstVar>)
      : SubFun<Sym<'srcVar>, Sym<'dstVar>> =
        Mapper.compose mapper (Mapper.cmake Reg)
        |> liftCToVSub
        |> liftVToSym
        |> onVars

    /// Converts an expression to its pre-state.
    let before
      : SubFun<Sym<Var>, Sym<MarkedVar>> =
        liftCToSymSub (Mapper.cmake Before)

    /// Converts an expression to its post-state.
    let after
      : SubFun<Sym<Var>, Sym<MarkedVar>> =
        liftCToSymSub (Mapper.cmake After)

    /// <summary>
    ///     Replaces symbols in a Boolean position with their
    ///     under-approximation.
    /// </summary>
    let approx
      : SubFun<Sym<MarkedVar>, Sym<MarkedVar>> =
        let rec boolSub pos v =
            (pos,
             match (pos, v) with
             | (Positions (position::_), Sym _) ->
                   Position.underapprox position
             | (Positions _, Reg x) -> BVar (Reg x)
             | _ -> failwith "approx must be used with Position context")
        and intSub pos v =
             match v with
             | Reg r -> (pos, AVar (Reg r))
             | Sym { Name = sym; Params = rs } ->
                 let pos', rs' = mapAccumL rmap pos rs
                 (pos', AVar (Sym { Name = sym; Params = rs' } ))
        and vsf = Mapper.makeCtx intSub boolSub
        and sf = onVars vsf
        and rmap ctx = Mapper.mapCtx sf (Position.push id ctx)

        sf


/// <summary>
///     Pretty printers for symbolics.
/// </summary>
module Pretty =
    open Starling.Core.Pretty
    open Starling.Core.Expr.Pretty
    open Starling.Core.Var.Pretty

    /// <summary>
    ///     Pretty-prints a <c>Sym</c>.
    /// </summary>
    /// <param name="pReg">
    ///     Pretty printer to use for regular variables.
    /// </param>
    /// <returns>
    ///     A function taking <c>Sym</c>s and returning pretty-printer
    ///     <c>Command</c>s.
    /// </returns>
    let rec printSym pReg =
        function
        | Sym { Name = sym ; Params = regs } ->
            func (sprintf "%%{%s}" sym) (Seq.map (printExpr (printSym pReg)) regs)
        | Reg reg -> pReg reg

    /// Pretty-prints a SVExpr.
    let printSVExpr = printExpr (printSym String)
    /// Pretty-prints a SMExpr.
    let printSMExpr = printExpr (printSym printMarkedVar)
    /// Pretty-prints a SVBoolExpr.
    let printSVBoolExpr = printBoolExpr (printSym String)
    /// Pretty-prints a SMBoolExpr.
    let printSMBoolExpr = printBoolExpr (printSym printMarkedVar)


/// <summary>
///     Tests for <c>Symbolic</c>.
/// </summary>
module Tests =
    open NUnit.Framework
    open Starling.Utils.Testing

    /// <summary>
    ///     NUnit tests for <c>Symbolic</c>.
    /// </summary>
    type NUnit () =
        /// Test cases for testing constant post-state rewriting.
        static member IntConstantPostStates =
            [ TestCaseData(siVar "target1")
                  .Returns(siAfter "target1")
                  .SetName("Rewrite single variable to post-state")
              TestCaseData(AAdd [AInt 4L; siVar "target1"])
                  .Returns(AAdd [AInt 4L; siAfter "target1"])
                  .SetName("Rewrite expression with one variable to post-state")
              TestCaseData(ASub [siVar "target1"; siVar "target2"])
                  .Returns(ASub [siAfter "target1"; siAfter "target2"])
                  .SetName("Rewrite expression with two variables to post-state")
              TestCaseData(ADiv (AInt 6L, AInt 0L) : SVIntExpr)
                  .Returns(ADiv (AInt 6L, AInt 0L) : SMIntExpr)
                  .SetName("Rewrite expression with no variables to post-state") ]

        [<TestCaseSource("IntConstantPostStates")>]
        /// Tests whether rewriting constants in arithmetic expressions to post-state works.
        member x.``constants in arithmetic expressions can be rewritten to post-state`` expr =
            expr |> Mapper.mapIntCtx after NoCtx |> snd

        /// <summary>
        ///     Test cases for testing underapproximation of Booleans.
        /// </summary>
        static member BoolApprox =
            [ (tcd
                   [| BAnd
                          [ bEq
                                (sbBefore "foo")
                                (sbAfter "bar")
                            BGt
                                (siBefore "baz", AInt 1L) ]
                      Position.positive |])
                  .Returns(
                    (Positions [ Positive ],
                     ((BAnd
                          [ bEq
                                (sbBefore "foo")
                                (sbAfter "bar")
                            BGt
                                (siBefore "baz", AInt 1L) ] ) : SMBoolExpr)))
                  .SetName("Don't alter +ve symbol-less expression")
              (tcd
                   [| BVar
                          (Sym
                               { Name = "test"
                                 Params = ([] : SMExpr list) } )
                      Position.positive |])
                  .Returns(
                      (Positions [ Positive ], (BFalse : SMBoolExpr)))
                  .SetName("Rewrite +ve param-less Bool symbol to false")
              (tcd
                   [| BVar
                          (Sym
                               { Name = "test"
                                 Params = ([] : SMExpr list) } )
                      Position.negative |])
                  .Returns(
                      (Positions [ Negative ], (BTrue : SMBoolExpr)))
                  .SetName("Rewrite -ve param-less Bool symbol to true")
              (tcd
                   [| BVar
                          (Sym { Name = "test"
                                 Params =
                                     ([ Expr.Int (siBefore "foo")
                                        Expr.Bool (sbAfter "bar") ] : SMExpr list) } )
                      Position.positive |])
                  .Returns(
                      (Positions [ Positive ], (BFalse : SMBoolExpr)))
                  .SetName("Rewrite +ve Reg-params Bool symbol to false")
              (tcd
                   [| BVar
                          (Sym { Name = "test"
                                 Params =
                                     ([ Expr.Int (siBefore "foo")
                                        Expr.Bool (sbAfter "bar") ] : SMExpr list) } )
                      Position.negative |])
                  .Returns(
                       (Positions [ Negative ], (BTrue : SMBoolExpr)))
                  .SetName("Rewrite -ve Reg-params Bool symbol to true")
              (tcd
                   [| BImplies
                          (BVar
                               (Sym { Name = "test1"
                                      Params =
                                          ([ Expr.Int (siBefore "foo")
                                             Expr.Bool (sbAfter "bar") ] : SMExpr list) } ),
                           BVar
                               (Sym { Name = "test2"
                                      Params =
                                          ([ Expr.Int (siBefore "baz")
                                             Expr.Bool (sbAfter "barbaz") ] : SMExpr list) } ))
                      Position.positive |])
                  .Returns(
                      (Positions [ Positive ],
                       BImplies
                           ((BTrue : SMBoolExpr),
                            (BFalse : SMBoolExpr))))
                  .SetName("Rewrite +ve implication correctly")
              (tcd
                   [| BImplies
                          (BVar
                               (Sym { Name = "test1"
                                      Params =
                                          ([ Expr.Int (siBefore "foo")
                                             Expr.Bool (sbAfter "bar") ] : SMExpr list) } ),
                           BVar
                               (Sym { Name = "test2"
                                      Params =
                                          ([ Expr.Int (siBefore "baz")
                                             Expr.Bool (sbAfter "barbaz") ] : SMExpr list) } ))
                      Position.negative |])
                  .Returns(
                      (Positions [ Negative ],
                       BImplies
                           ((BFalse : SMBoolExpr),
                            (BTrue : SMBoolExpr))))
                  .SetName("Rewrite -ve implication correctly") ]

        /// <summary>
        ///     Tests whether Boolean underapproximation works.
        /// </summary>
        [<TestCaseSource("BoolApprox")>]
        member this.testBoolApprox bl pos =
            bl |> Mapper.mapBoolCtx approx pos
