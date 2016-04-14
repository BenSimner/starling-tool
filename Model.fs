/// <summary>
///   Module of model types and functions.
/// </summary>
module Starling.Core.Model

open Chessie.ErrorHandling

open Starling.Collections
open Starling.Utils
open Starling.Core.Expr
open Starling.Core.Var


(*
 * Starling uses the following general terminology for model items.
 * (Note that these terms differ from their CamelCasedAndPrefixed
 * counterparts, whose meanings are given in their documentation comments.)
 *
 * func: a single term, usually Name(p1, p2, .., pn), in a view.
 *
 * view: an entire view expression, or multiset, of funcs.
 *
 * guarded: Starling represents case splits in its proof theory by
 *          surrounding a view or func whose presence in the proof is
 *          conditional with an expression true if or only if it is
 *          present; such a view or func is 'guarded'.
 *
 * view-set: a multiset of guarded views.
 *
 * conds: a pair of view assertions.
 *
 * axiom: a Hoare triple, containing a pair of conds, and some
 *        representation of a command.
 *
 * prim: a structured representation of an axiom command.
 *)


/// <summary>
///     Model types.
/// </summary>
[<AutoOpen>]
module Types =
    (*
     * Funcs (other than Starling.Collections.Func)
     *)

    /// A func over expressions, used in view expressions.
    type VFunc = Func<Expr>

    /// A view-definition func.
    type DFunc = Func<Type * string>


    (*
     * Views
     *)

    /// <summary>
    ///     A basic view, as a multiset of VFuncs.
    /// </summary>
    /// <remarks>
    ///     Though View is the canonical Concurrent Views Framework view,
    ///     we actually seldom use it.
    /// </remarks>
    type View = Multiset<VFunc>

    /// A view definition.
    type DView = List<DFunc>

    /// <summary>
    ///     A basic view, as an ordered list of VFuncs.
    /// </summary>
    type OView = List<VFunc>

    /// <summary>
    ///     A view expression, combining a view with its kind.
    /// </summary>
    /// <typeparam name="view">
    ///     The type of view wrapped inside this expression.
    /// </typeparam>
    type ViewExpr<'view> =
        /// <summary>
        ///     This view expression must be exercised by any proofs generated
        ///     by Starling.
        /// </summary>
        | Mandatory of 'view
        /// <summary>
        ///     This view expression may be elided in any proofs generated by
        ///     Starling.
        /// </summary>
        | Advisory of 'view

    (*
     * View definitions
     *)

    /// <summary>
    ///     A view definition.
    /// </summary>
    /// <typeparam name="view">
    ///     The type of views.
    /// </typeparam>
    /// <typeparam name="def">
    ///     The type of the definitions of said views.
    /// </typeparam>
    /// <remarks>
    ///     The semantics of a ViewDef is that, if Def is present, then the
    ///     view View is satisfied if, and only if, Def holds.
    /// </remarks>
    type ViewDef<'view, 'def> =
          /// <summary>
          ///     A definite <c>ViewDef</c>.
          /// </summary>
        | Definite of ('view * 'def)
          /// <summary>
          ///     An indefinite <c>ViewDef</c>.
          /// </summary>
        | Indefinite of 'view
          /// <summary>
          ///     An uninterpreted <c>ViewDef</c>.
          /// </summary>
        | Uninterpreted of 'view * string

    /// <summary>
    ///     A view definition over <c>BoolExpr</c>s.
    /// </summary>
    /// <typeparam name="view">
    ///     The type of views.
    /// </typeparam>
    type BViewDef<'view> = ViewDef<'view, BoolExpr>

    /// <summary>
    ///     Extracts the view of a <c>ViewDef</c>.
    /// </summary>
    let viewOf =
        function
        | Definite (v, _)
        | Uninterpreted (v, _)
        | Indefinite v -> v

    /// <summary>
    ///     Active pattern extracting the view of a <c>ViewDef</c>.
    /// </summary>
    let (|DefOver|) = viewOf

    (*
     * Terms
     *)

    /// <summary>
    ///     A term, containing a command relation, weakest precondition, and
    ///     goal.
    /// </summary>
    /// <remarks>
    ///     Though these are similar to Axioms, we keep them separate for
    ///     reasons of semantics: Axioms are literal Hoare triples {P}C{Q},
    ///     whereas Terms are some form of the actual Views axiom soundness
    ///     check we intend to prove.
    /// </remarks>
    type Term<'cmd, 'wpre, 'goal> =
        { /// The command relation of the Term.
          Cmd : 'cmd
          /// The weakest precondition of the Term.
          WPre : 'wpre
          /// The intended goal of the Term, ie the frame to preserve.
          Goal : 'goal
        }

    /// A term over semantic-relation commands.
    type STerm<'wpre, 'goal> = Term<BoolExpr, 'wpre, 'goal>

    /// A term using only internal boolean expressions.
    type FTerm = STerm<BoolExpr, BoolExpr>

    (*
     * Models
     *)

    /// A parameterised model of a Starling program.
    type Model<'axiom, 'viewdefs> =
        { Globals : VarMap
          Locals : VarMap
          Axioms : Map<string, 'axiom>
          /// <summary>
          ///     The semantic function for this model.
          /// </summary>
          Semantics : (DFunc * BoolExpr) list
          // This corresponds to the function D.
          ViewDefs : 'viewdefs }

    /// <summary>
    ///     A <c>Model</c> whose view definitions map <c>DView</c>s
    ///     through <c>ViewDef</c>s.
    ///     <c>Indefinite</c> bodies.
    /// </summary>
    /// <typeparam name="axiom">
    ///     Type of program axioms.
    /// </typeparam>
    type UVModel<'axiom> = Model<'axiom, BViewDef<DView> list>

    /// <summary>
    ///     A <c>Model</c> whose view definitions map <c>DFunc</c>s
    ///     through <c>ViewDef</c>s.
    /// </summary>
    /// <typeparam name="axiom">
    ///     Type of program axioms.
    /// </typeparam>
    type UFModel<'axiom> = Model<'axiom, BViewDef<DFunc> list>

/// <summary>
///     Pretty printers for the model.
/// </summary>
module Pretty =
    open Starling.Core.Pretty
    open Starling.Core.Var.Pretty
    open Starling.Core.Expr.Pretty

    /// Pretty-prints a type-name parameter.
    let printParam (ty, name) =
        hsep [ ty |> printType
               name |> String ]

    /// Pretty-prints a multiset given a printer for its contents.
    let printMultiset pItem =
        Multiset.toFlatList
        >> List.map pItem
        >> semiSep

    /// Pretty-prints a VFunc.
    let printVFunc = printFunc printExpr

    /// Pretty-prints a DFunc.
    let printDFunc = printFunc printParam

    /// Pretty-prints a View.
    let printView = printMultiset printVFunc

    /// Pretty-prints an OView.
    let printOView = List.map printVFunc >> semiSep  >> squared

    /// Pretty-prints a DView.
    let printDView = List.map printDFunc >> semiSep  >> squared

    /// Pretty-prints view expressions.
    let rec printViewExpr pView =
        function
        | Mandatory v -> pView v
        | Advisory v -> hjoin [ pView v ; String "?" ]

    /// Pretty-prints a term, given printers for its commands and views.
    let printTerm pCmd pWPre pGoal {Cmd = c; WPre = w; Goal = g} =
        vsep [ headed "Command" (c |> pCmd |> Seq.singleton)
               headed "W/Prec" (w |> pWPre |> Seq.singleton)
               headed "Goal" (g |> pGoal |> Seq.singleton) ]

    /// Pretty-prints an STerm.
    let printSTerm pWPre pGoal = printTerm printBoolExpr pWPre pGoal

    /// Pretty-prints model variables.
    let printModelVar (name, ty) =
        colonSep [ String name
                   printType ty ]

    /// <summary>
    ///     Pretty-prints an uninterpreted symbol.
    /// </summary>
    /// <param name="s">
    ///     The value of the symbol.
    /// </param>
    /// <returns>
    ///     A command printing <c>%{s}</c>.
    /// </returns>
    let printSymbol s =
        hjoin [ String "%" ; s |> String |> braced ]

    /// Pretty-prints a model constraint.
    let printViewDef pView pDef =
        function
        | Definite (vs, e) ->
            printAssoc Inline
                [ (String "View", pView vs)
                  (String "Def", pDef e) ]
        | Uninterpreted (vs, e) ->
            printAssoc Inline
                [ (String "View", pView vs)
                  (String "Def", printSymbol e) ]
        | Indefinite vs ->
            printAssoc Inline
                [ (String "View", pView vs)
                  (String "Def", String "?") ]

    /// Pretty-printer for BViewDefs.
    let printBViewDef pView =
        printViewDef pView printBoolExpr

    /// Pretty-prints the axiom map for a model.
    let printModelAxioms pAxiom model =
        printMap Indented String pAxiom model.Axioms

    /// Pretty-prints a model given axiom and defining-view printers.
    let printModel pAxiom pViewDefs model =
        headed "Model"
            [ headed "Shared variables" <|
                  Seq.singleton
                      (printMap Inline String printType model.Globals)
              headed "Thread variables" <|
                  Seq.singleton
                      (printMap Inline String printType model.Locals)
              headed "ViewDefs" <|
                  pViewDefs model.ViewDefs
              headed "Axioms" <|
                  Seq.singleton (printModelAxioms pAxiom model) ]


    /// <summary>
    ///     Enumerations of ways to view part or all of a <c>Model</c>.
    /// </summary>
    type ModelView =
        /// <summary>
        ///     View the entire model.
        /// </summary>
        | Model
        /// <summary>
        ///     View the model's terms.
        /// </summary>
        | Terms
        /// <summary>
        ///     View a specific term.
        /// </summary>
        | Term of string

    /// <summary>
    ///     Prints a model using the <c>ModelView</c> given.
    /// </summary>
    /// <param name="pAxiom">
    ///     The printer to use for model axioms.
    /// </param>
    /// <param name="pViewDef">
    ///     The printer to use for view definitions.
    /// </param>
    /// <param name="mview">
    ///     The <c>ModelView</c> stating which part of the model should be
    ///     printed.
    /// </param>
    /// <param name="model">
    ///     The model to print.
    /// </param>
    /// <returns>
    ///     A pretty-printer command printing the part of
    ///     <paramref name="model" /> specified by
    ///     <paramref name="mView" />.
    /// </returns>
    let printModelView pAxiom pViewDef mView m =
        match mView with
        | ModelView.Model -> printModel pAxiom pViewDef m
        | ModelView.Terms -> printModelAxioms pAxiom m
        | ModelView.Term termstr ->
            Map.tryFind termstr m.Axioms
            |> Option.map pAxiom
            |> withDefault (termstr |> sprintf "no term '%s'" |> String)

    /// <summary>
    ///     Pretty-prints a model view for an <c>UVModel</c>.
    /// </summary>
    /// <param name="pAxiom">
    ///     Pretty printer for axioms.
    /// </param>
    /// <returns>
    ///     A function, taking a <c>ModelView</c> and <c>UVModel</c>, and
    ///     returning a <c>Command</c>.
    /// </returns>
    let printUVModelView pAxiom =
        printModelView pAxiom (List.map (printBViewDef printDView))

    /// <summary>
    ///     Pretty-prints a model view for an <c>UFModel</c>.
    /// </summary>
    /// <param name="pAxiom">
    ///     Pretty printer for axioms.
    /// </param>
    /// <returns>
    ///     A function, taking a <c>ModelView</c> and <c>UFModel</c>, and
    ///     returning a <c>Command</c>.
    /// </returns>
    let printUFModelView pAxiom =
        printModelView pAxiom (List.map (printBViewDef printDFunc))


/// <summary>
///     Type-constrained version of <c>func</c> for <c>DFunc</c>s.
/// </summary>
/// <parameter name="name">
///     The name of the <c>DFunc</c>.
/// </parameter>
/// <parameter name="pars">
///     The parameters of the <c>DFunc</c>, as a sequence.
/// </parameter>
/// <returns>
///     A new <c>DFunc</c> with the given name and parameters.
/// </returns>
let dfunc name (pars : (Type * string) seq) : DFunc = func name pars

/// <summary>
///     Type-constrained version of <c>func</c> for <c>VFunc</c>s.
/// </summary>
/// <parameter name="name">
///     The name of the <c>VFunc</c>.
/// </parameter>
/// <parameter name="pars">
///     The parameters of the <c>VFunc</c>, as a sequence.
/// </parameter>
/// <returns>
///     A new <c>VFunc</c> with the given name and parameters.
/// </returns>
let vfunc name (pars : Expr seq) : VFunc = func name pars

/// Rewrites a Term by transforming its Cmd with fC, its WPre with fW,
/// and its Goal with fG.
let mapTerm fC fW fG {Cmd = c; WPre = w; Goal = g} =
    {Cmd = fC c; WPre = fW w; Goal = fG g}

/// Rewrites a Term by transforming its Cmd with fC, its WPre with fW,
/// and its Goal with fG.
/// fC, fW and fG must return Chessie results; liftMapTerm follows suit.
let tryMapTerm fC fW fG {Cmd = c; WPre = w; Goal = g} =
    trial {
        let! cR = fC c;
        let! wR = fW w;
        let! gR = fG g;
        return {Cmd = cR; WPre = wR; Goal = gR}
    }

/// Returns the axioms of a model.
let axioms {Axioms = xs} = xs

/// Creates a new model that is the input model with a different axiom set.
/// The axiom set may be of a different type.
let withAxioms (xs : Map<string, 'y>) (model : Model<'x, 'dview>)
    : Model<'y, 'dview> =
    { Globals = model.Globals
      Locals = model.Locals
      ViewDefs = model.ViewDefs
      Semantics = model.Semantics
      Axioms = xs }

/// Maps a pure function f over the axioms of a model.
let mapAxioms (f : 'x -> 'y) (model : Model<'x, 'dview>) : Model<'y, 'dview> =
    withAxioms (model |> axioms |> Map.map (fun _ -> f)) model

/// Maps a failing function f over the axioms of a model.
let tryMapAxioms (f : 'x -> Result<'y, 'e>) (model : Model<'x, 'dview>)
    : Result<Model<'y, 'dview>, 'e> =
    lift (fun x -> withAxioms x model)
         (model
          |> axioms
          |> Map.toSeq
          |> Seq.map (fun (k, v) -> v |> f |> lift (mkPair k))
          |> collect
          |> lift Map.ofList)

/// Returns the viewdefs of a model.
let viewDefs {ViewDefs = ds} = ds

/// Creates a new model that is the input model with a different viewdef set.
/// The viewdef set may be of a different type.
let withViewDefs (ds : 'y)
                 (model : Model<'axiom, 'x>)
                 : Model<'axiom, 'y> =
    { Globals = model.Globals
      Locals = model.Locals
      ViewDefs = ds
      Semantics = model.Semantics
      Axioms = model.Axioms }

/// Maps a pure function f over the viewdef database of a model.
let mapViewDefs (f : 'x -> 'y) (model : Model<'axiom, 'x>) : Model<'axiom, 'y> =
    withViewDefs (model |> viewDefs |> f) model

/// Maps a failing function f over the viewdef database of a model.
let tryMapViewDefs (f : 'x -> Result<'y, 'e>) (model : Model<'axiom, 'x>)
    : Result<Model<'axiom, 'y>, 'e> =
    lift (fun x -> withViewDefs x model) (model |> viewDefs |> f)


/// <summary>
///     Active pattern extracting a View from a ViewExpr.
/// </summary>
let (|InnerView|) =
    function
    | Advisory v | Mandatory v -> v

/// <summary>
///     Returns true if a <c>ViewExpr</c> can be removed at will without
///     invalidating the proof.
/// </summary>
/// <param name="_arg1">
///     The <c>ViewExpr</c> to query.
/// </param>
/// <returns>
///     True if <paramref name="_arg1" /> is <c>Advisory</c> or
///     <c>Unknown</c>.
/// </returns>
let isAdvisory =
    function
    | Advisory _ -> true
    | Mandatory _ -> false


(*
 * Variable querying.
 *)

/// <summary>
///     Extracts a sequence of all variables in a <c>VFunc</c>.
/// </summary>
/// <param name="_arg1">
///     The <c>VFunc</c> to query.
/// </param>
/// <returns>
///     A sequence of (<c>Const</c>, <c>Type</c>) pairs that represent all of
///     the variables used in the <c>VFunc</c>.
/// </returns>
let varsInVFunc { Params = ps } =
    ps |> Seq.map varsIn |> Seq.concat

