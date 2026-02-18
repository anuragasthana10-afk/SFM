namespace CRM.Models.Clients
{
    using CRM.Classes.Helpers;
    using CRM.Models.BusinessCodeDefinition;
    using CRM.Models.Workflows;
    using System.Data.Common;
    using System.Data.Entity;

    public partial class ClientModel : Business.Clients.ClientContext
    {
        public ClientModel()
            : base("name=DBConnection")
        {
            init();
        }

        public ClientModel(DbConnection existingConnection, DbTransaction existingTransaction = null)
            : base(existingConnection, existingTransaction)
        {
            init();
        }

        void init()
        {
            /***** Workflow Entities ******/
            Workflows = new FilteredDbSet<Workflow>(this, c => (c.DelFlag ?? false) == false);
            Workflow_Steps = new FilteredDbSet<Workflow_Steps>(this, c => (c.DelFlag ?? false) == false);
            Workflow_StepTransactions = new FilteredDbSet<Workflow_StepTransactions>(this, c => (c.DelFlag ?? false) == false);
            Workflow_StepTransitions = new FilteredDbSet<Workflow_StepTransition>(this, c => (c.DelFlag ?? false) == false && (c.IsActive ?? true));
            Workflow_Transactions_Headers = new FilteredDbSet<Workflow_Transactions_Headers>(this, c => (c.DelFlag ?? false) == false);
        }

        /***** Workflow Entities ******/
        public virtual IDbSet<Workflow_Steps> Workflow_Steps { get; set; }
        public virtual IDbSet<Workflow_StepTransactions> Workflow_StepTransactions { get; set; }
        public virtual IDbSet<Workflow_StepTransition> Workflow_StepTransitions { get; set; }
        public virtual IDbSet<Workflow_StepTypes> Workflow_StepTypes { get; set; }
        public virtual IDbSet<Workflow_Transactions_Headers> Workflow_Transactions_Headers { get; set; }
        public virtual IDbSet<Workflow> Workflows { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Stop doing table hash check to ignore manual changes to table definition.
            Database.SetInitializer<BusinessCodeDefinitionModel>(null);
            base.OnModelCreating(modelBuilder);

            /*** Workflow Entities ***/
            modelBuilder.Entity<Workflow_Steps>()
                .HasMany(e => e.Workflow_Steps1)
                .WithOptional(e => e.Workflow_Steps_NextStep)
                .HasForeignKey(e => e.Workflow_Steps_NextStep_ID);

            modelBuilder.Entity<Workflow_Steps>()
                .HasMany(e => e.Workflow_Steps11)
                .WithOptional(e => e.Workflow_Steps_PreviousStep)
                .HasForeignKey(e => e.Workflow_Steps_PreviousStep_ID);

            modelBuilder.Entity<Workflow_Steps>()
                .HasMany(e => e.Workflow_StepTransactions)
                .WithRequired(e => e.Workflow_Steps)
                .HasForeignKey(e => e.Workflow_Steps_ID)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Workflow_StepTransactions>()
                .Property(e => e.AssignedBy_Users_Time)
                .HasPrecision(2);

            modelBuilder.Entity<Workflow_StepTransactions>()
                .Property(e => e.ActionedBy_Users_Time)
                .HasPrecision(2);

            modelBuilder.Entity<Workflow_StepTransactions>()
                .HasMany(e => e.Workflow_StepTransactionsList)
                .WithOptional(e => e.Workflow_StepTransaction)
                .HasForeignKey(e => e.Previous_Workflow_StepTransactions_ID);

            modelBuilder.Entity<Workflow_StepTransition>()
                .Property(e => e.ConditionExpression)
                .IsUnicode(false);

            modelBuilder.Entity<Workflow_StepTransition>()
                .Property(e => e.DisplayLabel)
                .IsUnicode(false);

            modelBuilder.Entity<Workflow_StepTransition>()
                .HasRequired(e => e.Workflow)
                .WithMany(e => e.Workflow_StepTransitions)
                .HasForeignKey(e => e.Workflows_ID)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Workflow_StepTransition>()
                .HasRequired(e => e.From_Workflow_Step)
                .WithMany(e => e.Workflow_StepTransitions_From)
                .HasForeignKey(e => e.From_Workflow_Steps_ID)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Workflow_StepTransition>()
                .HasRequired(e => e.To_Workflow_Step)
                .WithMany(e => e.Workflow_StepTransitions_To)
                .HasForeignKey(e => e.To_Workflow_Steps_ID)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Workflow_StepTypes>()
                .Property(e => e.Description)
                .IsUnicode(false);

            modelBuilder.Entity<Workflow_StepTypes>()
                .HasMany(e => e.Workflow_Steps)
                .WithRequired(e => e.Workflow_StepType)
                .HasForeignKey(e => e.Workflow_StepTypes_ID)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Workflow_Transactions_Headers>()
                .Property(e => e.WorkflowComplete_Time)
                .HasPrecision(2);

            modelBuilder.Entity<Workflow_Transactions_Headers>()
                .HasMany(e => e.Workflow_StepTransactions)
                .WithRequired(e => e.Workflow_Transactions_Headers)
                .HasForeignKey(e => e.Workflow_Transactions_Headers_ID)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Workflow_Transactions_Headers>()
                .HasMany(e => e.Dependendent_Workflow_Transactions_Headers_List)
                .WithOptional(e => e.DependsOn_Workflow_Transaction_Header)
                .HasForeignKey(e => e.DependsOn_WorkFlows_Workflow_Transactions_Headers_ID);

            modelBuilder.Entity<Workflow>()
                .HasMany(e => e.Workflow_Steps)
                .WithRequired(e => e.Workflow)
                .HasForeignKey(e => e.Workflows_ID)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Workflow>()
                .HasMany(e => e.Workflow_Transactions_Headers)
                .WithRequired(e => e.Workflow)
                .HasForeignKey(e => e.Workflows_ID)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Workflow>()
                .HasMany(e => e.DependsOn_Workflows_List)
                .WithOptional(e => e.DependsOn_Workflow)
                .HasForeignKey(e => e.DependsOn_Workflows_ID);
        }
    }
}
