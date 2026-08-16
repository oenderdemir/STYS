import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { AgentInstallationWizardComponent } from './agent-installation-wizard.component';
import { AgentYonetimiService } from './agent-yonetimi.service';
import { TesisYonetimiService } from '../tesis-yonetimi/tesis-yonetimi.service';

describe('AgentInstallationWizardComponent', () => {
    let component!: AgentInstallationWizardComponent;
    let agentServiceSpy!: jasmine.SpyObj<AgentYonetimiService>;

    beforeEach(() => {
        agentServiceSpy = jasmine.createSpyObj<AgentYonetimiService>('AgentYonetimiService', ['createInstallation', 'getInstallations']);
        agentServiceSpy.createInstallation.and.returnValue(of({
            session: {
                id: 7,
                kurumId: 1,
                tesisId: 42,
                tesisAd: 'TRT Merkez',
                agentDisplayName: 'TRT Merkez Agent',
                targetRid: 'win-x64',
                scopes: ['agent.heartbeat'],
                status: 3,
                expiresAt: new Date().toISOString(),
                createdAt: new Date().toISOString()
            },
            enrollmentCode: 'ABCD-EFGH'
        }));
        agentServiceSpy.getInstallations.and.returnValue(of([]));

        TestBed.configureTestingModule({
            imports: [AgentInstallationWizardComponent],
            providers: [
                { provide: AgentYonetimiService, useValue: agentServiceSpy },
                {
                    provide: TesisYonetimiService,
                    useValue: {
                        getTesisler: () => of([{ id: 42, ad: 'TRT Merkez' }]),
                    }
                },
                ConfirmationService,
                MessageService
            ]
        });

        component = TestBed.createComponent(AgentInstallationWizardComponent).componentInstance;
        component.tesisler = [{ id: 42, ad: 'TRT Merkez' } as any];
        component.wizardForm = {
            tesisId: 42,
            agentDisplayName: 'TRT Merkez Agent',
            targetRid: 'win-x64',
            scopes: ['agent.heartbeat', 'agent.command.read', 'agent.command.execute', 'agent.result.write', 'agent.config.read'],
            requiresApproval: false
        };
    });

    it("createSession enrollment kodunu browser storage'a yazmaz", () => {
        const storageSpy = spyOn(localStorage, 'setItem');

        component.createSession();
        component.createSession();

        expect(agentServiceSpy.createInstallation).toHaveBeenCalledTimes(1);
        expect(component.generatedEnrollmentCode()).toBe('ABCD-EFGH');
        expect(storageSpy).not.toHaveBeenCalled();
        expect(component.isCreationLocked()).toBeTrue();
    });

    it('kurulum oturumu paket adimindan once olusur', () => {
        // The package step can only download once a session exists, so session creation has to
        // happen on the step before it. Previously the create button lived on the step AFTER the
        // package step, which left the download button permanently disabled on first pass and
        // forced the operator to navigate backwards.
        expect(component.selectedSession()).toBeNull();

        component.createSession();

        expect(component.selectedSession()).not.toBeNull();
        expect(component.wizardStep()).toBe(5);
        expect(component.generatedEnrollmentCode()).toBe('ABCD-EFGH');
    });
});
