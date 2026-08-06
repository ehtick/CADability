using CADability.GeoObject;

namespace CADability.Tests
{
    /// <summary>
    /// Regression tests for undoing operations that take a whole <see cref="GeoObjectList"/>.
    /// GeoObjectList has an implicit conversion to IGeoObject[], which converts to object[] by array
    /// covariance. A single GeoObjectList argument therefore binds to the "params object[]" constructor
    /// of <see cref="ReversibleChange"/> and used to be spread into one parameter per contained object.
    /// The undo then looked for a method taking n objects, did not find one and silently did nothing,
    /// so deleting more than one object could not be undone.
    /// </summary>
    [TestClass]
    public class UndoRedoTests
    {
        private static Line MakeLine(double x)
        {
            Line line = Line.Construct();
            line.SetTwoPoints(new GeoPoint(x, 0, 0), new GeoPoint(x + 10, 5, 0));
            return line;
        }

        private static Project MakeProject(int lineCount, out Model model)
        {
            Project project = Project.CreateSimpleProject();
            model = project.GetActiveModel();
            for (int i = 0; i < lineCount; i++) model.Add(MakeLine(i * 20));
            project.Undo.Clear();
            return project;
        }

        /// <summary>
        /// Does what SelectObjectsAction does for the menu command "MenuId.Object.Delete".
        /// </summary>
        private static void DeleteFromModel(Project project, Model model, GeoObjectList selectedObjects)
        {
            using (project.Undo.UndoFrame)
            {
                GeoObjectList toRemoveFromModel = new GeoObjectList();
                for (int i = 0; i < selectedObjects.Count; i++)
                {
                    if (selectedObjects[i].Owner == model) toRemoveFromModel.Add(selectedObjects[i]);
                }
                if (toRemoveFromModel.Count > 0) model.Remove(toRemoveFromModel);
            }
        }

        [DataTestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        public void UndoOfDelete_RestoresAllDeletedObjects(int deleteCount)
        {
            Project project = MakeProject(5, out Model model);
            GeoObjectList toDelete = new GeoObjectList();
            for (int i = 0; i < deleteCount; i++) toDelete.Add(model[i]);

            DeleteFromModel(project, model, toDelete);
            Assert.AreEqual(5 - deleteCount, model.Count, "objects were not deleted");

            Assert.IsTrue(project.Undo.UndoLastStep());
            Assert.AreEqual(5, model.Count, "undo did not restore all deleted objects");
        }

        [DataTestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        public void RedoOfDelete_RemovesTheObjectsAgain(int deleteCount)
        {
            Project project = MakeProject(5, out Model model);
            GeoObjectList toDelete = new GeoObjectList();
            for (int i = 0; i < deleteCount; i++) toDelete.Add(model[i]);

            DeleteFromModel(project, model, toDelete);
            project.Undo.UndoLastStep();

            Assert.IsTrue(project.Undo.RedoLastStep());
            Assert.AreEqual(5 - deleteCount, model.Count, "redo did not remove the objects again");
        }

        [TestMethod]
        public void UndoOfDelete_StillWorksAfterAPreviousUndo()
        {
            // the scenario as reported: delete a single line, undo it, then delete two lines and undo that
            Project project = MakeProject(5, out Model model);

            GeoObjectList first = new GeoObjectList();
            first.Add(model[0]);
            DeleteFromModel(project, model, first);
            project.Undo.UndoLastStep();
            Assert.AreEqual(5, model.Count);

            GeoObjectList second = new GeoObjectList();
            second.Add(model[0]);
            second.Add(model[1]);
            DeleteFromModel(project, model, second);
            project.Undo.UndoLastStep();
            Assert.AreEqual(5, model.Count, "undo of the second delete did not restore the objects");
        }

        [DataTestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        public void UndoOfModelAddList_RemovesAllAddedObjects(int addCount)
        {
            Project project = MakeProject(0, out Model model);
            GeoObjectList toAdd = new GeoObjectList();
            for (int i = 0; i < addCount; i++) toAdd.Add(MakeLine(i * 20));

            model.Add(toAdd);
            Assert.AreEqual(addCount, model.Count);

            Assert.IsTrue(project.Undo.UndoLastStep());
            Assert.AreEqual(0, model.Count, "undo did not remove all added objects");
        }

        [DataTestMethod]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        public void UndoOfBlockAddList_RemovesAllAddedObjects(int addCount)
        {
            Project project = MakeProject(0, out Model model);
            Block block = Block.Construct();
            model.Add(block);
            project.Undo.Clear();

            GeoObjectList toAdd = new GeoObjectList();
            for (int i = 0; i < addCount; i++) toAdd.Add(MakeLine(i * 20));

            block.Add(toAdd);
            Assert.AreEqual(addCount, block.Count);

            Assert.IsTrue(project.Undo.UndoLastStep());
            Assert.AreEqual(0, block.Count, "undo did not remove all objects added to the block");
        }

        [TestMethod]
        public void ReversibleChange_KeepsAGeoObjectListAsASingleParameter()
        {
            // the root cause: a GeoObjectList must not be spread into one parameter per contained object
            Model model = new Model();
            GeoObjectList list = new GeoObjectList();
            list.Add(MakeLine(0));
            list.Add(MakeLine(20));

            ReversibleChange change = new ReversibleChange(model, "Add", list);

            Assert.AreEqual(1, change.Parameters.Length, "the list was spread into several parameters");
            Assert.AreSame(list, change.Parameters[0]);
        }
    }
}
