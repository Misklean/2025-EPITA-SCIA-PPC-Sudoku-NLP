using System.Collections.Generic;
using Microsoft.Z3;
using Sudoku.Shared;

namespace Sudoku.Z3copy
{
    public class Z3copy : ISudokuSolver
    {
        private BitVecExpr[][] CreateEvalMatrix(Context ctx)
        {
            BitVecExpr[][] X = new BitVecExpr[9][];

            for (uint i = 0; i < 9; i++)
            {
                X[i] = new BitVecExpr[9];
                for (uint j = 0; j < 9; j++)
                    X[i][j] = ctx.MkBVConst(ctx.MkSymbol("x_" + (i + 1) + "_" + (j + 1)), 4);
            }

            return X;
        }

        private Expr[][] EvalCell(Context ctx, BitVecExpr[][] X)
        {
            Expr[][] cells_c = new Expr[9][];
            BitVecNum one = ctx.MkBV(1, 4);
            BitVecNum nine = ctx.MkBV(9, 4);

            for (uint i = 0; i < 9; i++)
            {
                cells_c[i] = new BoolExpr[9];
                for (uint j = 0; j < 9; j++)
                    cells_c[i][j] = ctx.MkAnd(ctx.MkBVULE(one, X[i][j]), ctx.MkBVULE(X[i][j], nine));
            }

            return cells_c;
        }

        private BoolExpr[] EvalRow(Context ctx, BitVecExpr[][] X)
        {
            BoolExpr[] rows_c = new BoolExpr[9];

            for (uint i = 0; i < 9; i++)
                rows_c[i] = ctx.MkDistinct(X[i]);

            return rows_c;
        }

        private BoolExpr[] EvalCol(Context ctx, BitVecExpr[][] X)
        {
            BoolExpr[] cols_c = new BoolExpr[9];

            for (uint j = 0; j < 9; j++)
            {
                BitVecExpr[] column = new BitVecExpr[9];
                for (uint i = 0; i < 9; i++)
                    column[i] = X[i][j];

                cols_c[j] = ctx.MkDistinct(column);
            }

            return cols_c;
        }

        private BoolExpr[][] EvalSquare(Context ctx, BitVecExpr[][] X)
        {
            BoolExpr[][] sq_c = new BoolExpr[3][];

            for (uint i0 = 0; i0 < 3; i0++)
            {
                sq_c[i0] = new BoolExpr[3];
                for (uint j0 = 0; j0 < 3; j0++)
                {
                    BitVecExpr[] square = new BitVecExpr[9];
                    for (uint i = 0; i < 3; i++)
                        for (uint j = 0; j < 3; j++)
                            square[3 * i + j] = X[3 * i0 + i][3 * j0 + j];

                    sq_c[i0][j0] = ctx.MkDistinct(square);
                }
            }

            return sq_c;
        }

        private BoolExpr EvalSudoku(Context ctx, Expr[][] cells_c, BoolExpr[] rows_c, BoolExpr[] cols_c, BoolExpr[][] sq_c)
        {
            BoolExpr sudoku_c = ctx.MkTrue();

            foreach (BoolExpr[] t in cells_c)
                sudoku_c = ctx.MkAnd(ctx.MkAnd(t), sudoku_c);

            sudoku_c = ctx.MkAnd(ctx.MkAnd(rows_c), sudoku_c);
            sudoku_c = ctx.MkAnd(ctx.MkAnd(cols_c), sudoku_c);

            foreach (BoolExpr[] t in sq_c)
                sudoku_c = ctx.MkAnd(ctx.MkAnd(t), sudoku_c);

            return sudoku_c;
        }

        private BoolExpr CreateSudokuToSolve(Context ctx, int[,] instance, BitVecExpr[][] X)
        {
            BoolExpr instance_c = ctx.MkTrue();

            for (uint i = 0; i < 9; i++)
                for (uint j = 0; j < 9; j++)
                    if (instance[i, j] != 0)
                    {
                        instance_c = ctx.MkAnd(instance_c,
                            ctx.MkEq(X[i][j], ctx.MkBV(instance[i, j], 4)));
                    }

            return instance_c;
        }

        private SudokuGrid SolveSudoku(Solver solver, BitVecExpr[][] X)
        {
            var sudoku_g = new SudokuGrid();
            Model m = solver.Model;

            for (uint i = 0; i < 9; i++)
                for (uint j = 0; j < 9; j++)
                {
                    var eval = m.Evaluate(X[i][j]) as BitVecNum;
                    sudoku_g.Cells[i, j] = (int)eval.UInt64;
                }

            return sudoku_g;
        }

        public SudokuGrid Solve(SudokuGrid s)
        {
            Context ctx = new Context(new Dictionary<string, string>() { { "model", "true" } });

            // 9x9 matrix of 4-bit vector variables
            BitVecExpr[][] X = CreateEvalMatrix(ctx);

            // each cell contains a value in {1, ..., 9} represented as a bit vector
            Expr[][] cells_c = EvalCell(ctx, X);

            // each row contains a digit at most once
            BoolExpr[] rows_c = EvalRow(ctx, X);

            // each column contains a digit at most once
            BoolExpr[] cols_c = EvalCol(ctx, X);

            // each 3x3 square contains a digit at most once
            BoolExpr[][] sq_c = EvalSquare(ctx, X);

            // evaluate the sudoku with all the previous Evaluations
            BoolExpr sudoku_c = EvalSudoku(ctx, cells_c, rows_c, cols_c, sq_c);

            int[,] instance = s.Cells;

            BoolExpr instance_c = CreateSudokuToSolve(ctx, instance, X);

            Solver solver = ctx.MkSolver();
            solver.Assert(sudoku_c);
            solver.Assert(instance_c);

            if (solver.Check() != Status.SATISFIABLE)
            {
                return s;
            }

            return SolveSudoku(solver, X);
        }
    }
}
