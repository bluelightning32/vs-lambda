Puzzles
==============================

These are Coq questions that can be used as puzzles in the game.

## Function application

Puzzles that only need function application to solve. Furthermore, any instantiation of the type is sufficient.

* `unit * unit`
    * `pair tt tt`
* `(nat * unit) + (unit * nat)`
* `unit + unit`
    * `@inl unit unit tt`
* `unit + {True}`
    * `@inleft unit True tt`
    * `@inright unit True I`
* `{True} + {True}`
    * `@left True True I`
    * `@right True True I`
* `option unit`
    * `@None unit`
    * `Some tt`
* `option nat`
    * `@None nat`
    * `Some 0`
* `tt = tt`
    * `eq_refl tt`

## Build function of type

These puzzles require building functions, but any function of the specified type is accepted. The player must obtain the function type as a reward from some other activity, then plug the function type into the function builder. This way the complicated process of creating function types can be delayed until later in the game.

```
Definition identity: forall A, A -> A := fun A a => a.
```

```
Definition either: forall A, A -> A -> A := fun A a b => a.
```

```
Definition swap_and: forall A B, A * B -> B * A :=
  fun A B p =>
    match p with
    | pair a b => pair b a
    end.
```

```
Definition swap_or: forall A B, A + B -> B + A :=
  fun A B p =>
    match p with
    | inl a => @inr B A a
    | inr b => @inl B A b
    end.
```

K combinator - always returns the first of two non-type parameters.
```
Definition K_combinator: forall A B, A -> B -> A := fun A B a b => a.
```

B combinator - composes two functions together.
```
Definition B_combinator: forall A B C, (B -> C) -> (A -> B) -> A -> C :=
  fun A B C f g a => f (g a).
```

## Non-fixpoints with value validation

Add two.
```
nat -> nat.
Theorem adds_two: forall n, f n = S (S n).
```

Maybe increment.
```
nat -> bool -> nat.
Theorem true_increments: forall n, f n true = S n.
Theorem false_preserves: forall n, f n false = n.
```

Negate.
```
bool -> bool.
Theorem false_negated: f false = true.
Theorem true_negated: f true = false.
```

## Build fixpoint

```
Inductive Wrapped (A: Type): Type :=
| start (a: A): Wrapped A
| wrap (a: A) (w:Wrapped A): Wrapped A.

Definition get_last: forall A, Wrapped A -> A :=
fix f A a :=
match a with
| start _ a => a
| wrap _ a tl => f A tl
end.
```

## Fixpoints with value validation

```
forall A B, list (A * B) -> list A.
Theorem returns_first: forall A B l a b, get_list_fst A B ( (a, b) :: l) = a :: get_list_fst A B l.
```

```
Definition get_list_fst: forall A B, list (A * B) -> list A :=
  fix f A B l :=
  match l with
  | cons p l =>
      match p with
      | pair a b => cons a (f A B l)
      end
  | nil => nil
  end.
```

```
forall A B, list (A * B) -> list B.
Theorem returns_second: forall A B l a b, get_list_snd A B ( (a, b) :: l) = b :: get_list_nsd A B l.
```

```
forall A B, A -> list B -> list (A*B).
Theorem fst_const: forall A B a b l, make_const_fst_pair_list A B a (b:: l) = (a, b) :: make_const_fst_pair_list A B a l.
```

```
Definition make_const_fst_pair_list: forall A B, A -> list B -> list (A*B) :=
  fix f A B a l :=
  match l with
  | cons b l =>
      cons (a, b) (f A B a l)
  | nil =>
      nil
  end.
```

```
forall A B, list (A * B) -> list (B * A).
Theorem swap_pair_list_swaps: forall A B a b l, In (a, b) l <-> In (b, a) (swap_pair_list A B l).
```

```
Definition swap_pair_list: forall A B, list (A * B) -> list (B * A) :=
  fix f A B l :=
  match l with
  | cons p l =>
      match p with
      | pair a b => cons (pair b a) (f A B l)
      end
  | nil => nil
  end.
```
