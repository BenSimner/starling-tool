/// <summary>
///     The <c>Axiom</c> type, and mappings from <c>Graph</c>s to <c>Axiom</cs>.
///
///     <para>
///         As in Views, <c>Axiom</c>s in Starling are triples (p, c, q),
///         where p and q are views and c is some form of atomic command.
///         They are distinct from <c>Term</c>s, which model the
///         individual terms of an axiom soundness proof.
///     </para>
/// </summary>
module Starling.Axiom

open Starling.Model


(*
 * Types
 *)

/// A general Hoare triple, consisting of precondition, inner item, and
/// postcondition.
type Axiom<'view, 'cmd> = 
    { Pre : 'view
      Post : 'view
      Cmd : 'cmd }

/// An axiom with a VFunc as its command.
type PAxiom<'view> = Axiom<'view, VFunc>

/// An axiom combined with a framing guarded view.
type FramedAxiom = 
    { /// The axiom to be checked for soundness under Frame.
      Axiom : PAxiom<GView>
      /// The view to be preserved by Axiom.
      Frame : View }


(*
 * Functions
 *)

/// Makes an axiom {p}c{q}.
let axiom p c q =
    { Pre = p; Post = q; Cmd = c }